using System.Security.Cryptography;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Certificates;
using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Certificates;

public interface ICertificateService
{
    /// Every routed hostname plus every certificate row, merged — the page that manages HTTPS needs the
    /// hostnames that don't have it yet just as much as the ones that do.
    Task<TlsOverviewResponse> OverviewAsync(CancellationToken ct = default);

    /// Opts a hostname into HTTPS and requests its certificate straight away, so the operator sees the
    /// outcome in seconds rather than at tomorrow's renewal tick.
    Task<CertificateResponse> EnableAsync(string hostname, CancellationToken ct = default);

    /// Back to plain HTTP: the row goes, and the private key with it.
    Task DisableAsync(string hostname, CancellationToken ct = default);

    /// Try one hostname again now, rather than waiting for tomorrow's pass.
    Task<CertificateResponse> RetryAsync(string hostname, CancellationToken ct = default);

    /// Requests every row that needs one — never creates rows, so nothing is ever issued that wasn't
    /// asked for. Returns how many certificates actually changed, i.e. whether nginx needs reapplying.
    Task<int> IssueDueAsync(CancellationToken ct = default);

    /// What the edge can actually install right now: valid, unexpired, and decryptable. Internal —
    /// private keys in the clear never belong on an API shape.
    Task<IReadOnlyList<CertificateMaterial>> InstallableAsync(CancellationToken ct = default);
}

public sealed record CertificateMaterial
{
    public required string Hostname { get; init; }
    public required string PemChain { get; init; }
    public required string PrivateKeyPem { get; init; }
}

public sealed class CertificateService(
    ICertificateRepository certificates,
    IRouteRepository routes,
    IAcmeClient acme,
    ISecretProtector protector,
    IOptions<AcmeOptions> options,
    TimeProvider clock) : ICertificateService
{
    public async Task<TlsOverviewResponse> OverviewAsync(CancellationToken ct = default)
    {
        var rows = (await certificates.ListAsync(ct)).ToDictionary(c => c.Hostname, StringComparer.Ordinal);
        var counts = (await routes.ListAsync(ct))
            .GroupBy(r => r.Hostname, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var hostnames = counts.Keys.Union(rows.Keys, StringComparer.Ordinal).OrderBy(h => h, StringComparer.Ordinal);

        return new TlsOverviewResponse
        {
            StagingCa = !acme.UsesProductionCa,
            Hosts = [.. hostnames.Select(hostname =>
            {
                var certificate = rows.GetValueOrDefault(hostname);
                return new TlsHostResponse
                {
                    Hostname = hostname,
                    RouteCount = counts.GetValueOrDefault(hostname),
                    Status = certificate?.Status,
                    NotAfter = certificate?.NotAfter,
                    LastError = certificate?.LastError,
                    IssuanceBlockedReason = IssuableHostname.BlockedReason(hostname, options.Value.ReservedSuffixes),
                };
            })],
        };
    }

    public async Task<CertificateResponse> EnableAsync(string hostname, CancellationToken ct = default)
    {
        var host = ValidateHostname(hostname);

        if (IssuableHostname.BlockedReason(host, options.Value.ReservedSuffixes) is { } blocked)
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname), blocked);
        }

        if (await certificates.FindByHostnameAsync(host, ct) is not null)
        {
            throw new ConflictException($"HTTPS is already enabled for '{host}'.");
        }

        if (!(await routes.ListAsync(ct)).Any(r => r.Hostname == host))
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname),
                "Add a route for this hostname first — the CA validates by fetching from it over port 80.");
        }

        var now = clock.GetUtcNow();
        var certificate = new Certificate { Hostname = host, CreatedAt = now, UpdatedAt = now };
        await certificates.AddAsync(certificate, ct);

        await IssueAsync(certificate, ct);
        return Map(certificate);
    }

    public async Task DisableAsync(string hostname, CancellationToken ct = default)
    {
        var host = ValidateHostname(hostname);
        var certificate = await certificates.FindByHostnameAsync(host, ct)
            ?? throw new NotFoundException($"HTTPS is not enabled for '{host}'.");

        await certificates.RemoveAsync(certificate, ct);
    }

    public async Task<CertificateResponse> RetryAsync(string hostname, CancellationToken ct = default)
    {
        var host = ValidateHostname(hostname);
        var certificate = await certificates.FindByHostnameAsync(host, ct)
            ?? throw new NotFoundException($"HTTPS is not enabled for '{host}'.");

        await IssueAsync(certificate, ct);
        return Map(certificate);
    }

    public async Task<int> IssueDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var changed = 0;

        foreach (var certificate in await certificates.ListAsync(ct))
        {
            if (!CertificateRenewal.IsDue(certificate, now, options.Value.RenewBeforeDays))
            {
                continue;
            }

            var before = certificate.PemChain;
            await IssueAsync(certificate, ct);
            if (certificate.PemChain != before)
            {
                changed++;
            }
        }

        return changed;
    }

    public async Task<IReadOnlyList<CertificateMaterial>> InstallableAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var installable = new List<CertificateMaterial>();

        foreach (var certificate in await certificates.ListAsync(ct))
        {
            if (certificate.Status != CertificateStatus.Valid
                || certificate.PemChain is not { Length: > 0 } chain
                || certificate.PrivateKeyEnc is not { Length: > 0 } key
                // Expired but not yet retried (the Host was off, say). Plain HTTP beats handing every
                // visitor a certificate error.
                || certificate.NotAfter <= now)
            {
                continue;
            }

            string plaintextKey;
            try
            {
                plaintextKey = protector.Unprotect(key);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // Restored database, different encryption key. Skipping keeps the edge serving HTTP for
                // this hostname instead of failing the whole apply; renewal re-issues it.
                continue;
            }

            installable.Add(new CertificateMaterial
            {
                Hostname = certificate.Hostname,
                PemChain = chain,
                PrivateKeyPem = plaintextKey,
            });
        }

        return installable;
    }

    private async Task IssueAsync(Certificate certificate, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        try
        {
            var issued = await acme.IssueAsync([certificate.Hostname], ct);
            certificate.PemChain = issued.PemChain;
            certificate.PrivateKeyEnc = protector.Protect(issued.PrivateKeyPem);
            certificate.IssuedAt = issued.NotBefore;
            certificate.NotAfter = issued.NotAfter;
            certificate.Status = CertificateStatus.Valid;
            certificate.LastError = null;
        }
        catch (CertificateIssuanceException ex)
        {
            // A renewal that fails while the current certificate is still good leaves it in place and
            // serving — there are ~30 days of retries left, and dropping the site to HTTP for a
            // transient CA failure would be the worse outcome. Once it expires it isn't valid any more,
            // and a hostname that never had one was never serving HTTPS to begin with.
            certificate.Status = certificate.PemChain is not null && certificate.NotAfter > now
                ? CertificateStatus.Valid
                : CertificateStatus.Failed;
            certificate.LastError = ex.Message;
        }

        certificate.UpdatedAt = now;
        await certificates.UpdateAsync(certificate, ct);
    }

    private static string ValidateHostname(string hostname)
    {
        var value = (hostname ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0 || value.Contains(' ') || value.Contains('/'))
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname), "A valid hostname is required.");
        }

        return value;
    }

    private static CertificateResponse Map(Certificate c) => new()
    {
        Hostname = c.Hostname,
        Status = c.Status,
        IssuedAt = c.IssuedAt,
        NotAfter = c.NotAfter,
        LastError = c.LastError,
    };
}
