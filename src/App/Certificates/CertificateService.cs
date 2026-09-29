using System.Security.Cryptography;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Certificates;
using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Certificates;

public interface ICertificateService
{
    /// includes routed hostnames without a cert yet
    Task<TlsOverviewResponse> OverviewAsync(CancellationToken ct = default);

    /// issues straight away so the operator sees the result now, not at the next tick
    Task<CertificateResponse> EnableAsync(string hostname, CancellationToken ct = default);

    /// deletes the row and the private key with it
    Task DisableAsync(string hostname, CancellationToken ct = default);

    Task<CertificateResponse> RetryAsync(string hostname, CancellationToken ct = default);

    /// www.{hostname} 301s to hostname, reissues straight away if there's a cert so it covers both
    Task SetWwwRedirectAsync(string hostname, bool enabled, CancellationToken ct = default);

    /// never creates rows, returns how many changed (nonzero means reapply nginx)
    Task<int> IssueDueAsync(CancellationToken ct = default);

    /// valid, unexpired and decryptable. keys are in the clear so keep this off the API
    Task<IReadOnlyList<CertificateMaterial>> InstallableAsync(CancellationToken ct = default);
}

public sealed record CertificateMaterial
{
    public required string Hostname { get; init; }
    public required string PemChain { get; init; }
    public required string PrivateKeyPem { get; init; }
    public bool IncludesWww { get; init; }
}

public sealed class CertificateService(
    ICertificateRepository certificates,
    IRouteRepository routes,
    IWwwRedirectRepository wwwRedirects,
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
        var www = await WwwHostsAsync(ct);

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
                    WwwRedirect = www.Contains(hostname),
                    CertificateIncludesWww = certificate?.IncludesWww ?? false,
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

        if (await wwwRedirects.FindAsync(host, ct) is not null
            && IssuableHostname.BlockedReason(Www(host), options.Value.ReservedSuffixes) is { } wwwBlocked)
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname), wwwBlocked);
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

    public async Task SetWwwRedirectAsync(string hostname, bool enabled, CancellationToken ct = default)
    {
        var host = ValidateHostname(hostname);
        var existing = await wwwRedirects.FindAsync(host, ct);

        if (!enabled)
        {
            if (existing is not null)
            {
                await wwwRedirects.RemoveAsync(existing, ct);
                await ReissueIfCertifiedAsync(host, ct);
            }

            return;
        }

        if (existing is not null)
        {
            return;
        }

        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname), "This is already a www hostname.");
        }

        var routed = await routes.ListAsync(ct);
        if (!routed.Any(r => r.Hostname == host))
        {
            throw new ValidationException(nameof(EnableHttpsRequest.Hostname), "Add a route for this hostname first.");
        }

        if (routed.Any(r => r.Hostname == Www(host)))
        {
            throw new ConflictException($"'{Www(host)}' has its own routes. Remove them before redirecting it to '{host}'.");
        }

        await wwwRedirects.AddAsync(new WwwRedirect { Hostname = host, CreatedAt = clock.GetUtcNow() }, ct);
        await ReissueIfCertifiedAsync(host, ct);
    }

    private async Task ReissueIfCertifiedAsync(string host, CancellationToken ct)
    {
        if (await certificates.FindByHostnameAsync(host, ct) is { } certificate)
        {
            await IssueAsync(certificate, ct);
        }
    }

    public async Task<int> IssueDueAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var changed = 0;
        var www = await WwwHostsAsync(ct);

        foreach (var certificate in await certificates.ListAsync(ct))
        {
            if (!CertificateRenewal.IsDue(certificate, now, options.Value.RenewBeforeDays, www.Contains(certificate.Hostname)))
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
                // expired but not retried yet, plain http beats a cert error
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
                // restored db with a different key, skip so the apply still works, renewal reissues
                continue;
            }

            installable.Add(new CertificateMaterial
            {
                Hostname = certificate.Hostname,
                PemChain = chain,
                PrivateKeyPem = plaintextKey,
                IncludesWww = certificate.IncludesWww,
            });
        }

        return installable;
    }

    private async Task IssueAsync(Certificate certificate, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        try
        {
            // www state read here so renewal, retry and the toggle all issue for the same names
            var includesWww = await wwwRedirects.FindAsync(certificate.Hostname, ct) is not null;
            var issued = await acme.IssueAsync(
                includesWww ? [certificate.Hostname, Www(certificate.Hostname)] : [certificate.Hostname], ct);
            certificate.PemChain = issued.PemChain;
            certificate.PrivateKeyEnc = protector.Protect(issued.PrivateKeyPem);
            certificate.IssuedAt = issued.NotBefore;
            certificate.NotAfter = issued.NotAfter;
            certificate.Status = CertificateStatus.Valid;
            certificate.LastError = null;
            certificate.IncludesWww = includesWww;
        }
        catch (CertificateIssuanceException ex)
        {
            // a failed renewal keeps a still-valid cert serving, dropping to http over a blip is worse
            certificate.Status = certificate.PemChain is not null && certificate.NotAfter > now
                ? CertificateStatus.Valid
                : CertificateStatus.Failed;
            certificate.LastError = ex.Message;
        }

        certificate.UpdatedAt = now;
        await certificates.UpdateAsync(certificate, ct);
    }

    private async Task<HashSet<string>> WwwHostsAsync(CancellationToken ct)
        => (await wwwRedirects.ListAsync(ct)).Select(w => w.Hostname).ToHashSet(StringComparer.Ordinal);

    private static string Www(string hostname) => $"www.{hostname}";

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
