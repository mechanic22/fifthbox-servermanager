using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Integrations.Certificates;

public sealed class CertesAcmeClient(
    IAcmeChallengeStore challenges,
    IAcmeAccountStore accounts,
    IPlatformSettingsRepository settings,
    ISecretProtector protector,
    IOptions<AcmeOptions> options,
    TimeProvider clock,
    ILogger<CertesAcmeClient> log) : IAcmeClient
{
    public bool UsesProductionCa => AcmeDirectories.IsProduction(options.Value.Directory);

    public async Task<IssuedCertificate> IssueAsync(IReadOnlyList<string> hostnames, CancellationToken ct = default)
    {
        if (hostnames.Count == 0)
        {
            throw new CertificateIssuanceException("A certificate needs at least one hostname.");
        }

        var acme = await ContextAsync(ct);
        var order = await acme.NewOrder([.. hostnames]);
        var published = new List<string>();

        try
        {
            foreach (var authorization in await order.Authorizations())
            {
                var challenge = await authorization.Http();
                challenges.Publish(challenge.Token, challenge.KeyAuthz);
                published.Add(challenge.Token);
                await challenge.Validate();
            }

            // kick off every validation, then wait, so it isn't a round of latency per hostname
            foreach (var authorization in await order.Authorizations())
            {
                await AwaitValidationAsync(authorization, ct);
            }

            var key = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var chain = await order.Generate(new CsrInfo { CommonName = hostnames[0] }, key);
            var pem = CertificateChainPem.Combine(chain.Certificate.ToPem(), chain.Issuers.Select(i => i.ToPem()));
            var (notBefore, notAfter) = CertificateChainReader.Validity(pem);

            log.LogInformation("Issued a certificate for {Hostnames} valid until {NotAfter}",
                string.Join(", ", hostnames), notAfter);

            return new IssuedCertificate
            {
                PemChain = pem,
                PrivateKeyPem = key.ToPem(),
                NotBefore = notBefore,
                NotAfter = notAfter,
            };
        }
        catch (AcmeRequestException ex)
        {
            throw new CertificateIssuanceException(ex.Error?.Detail ?? ex.Message, ex);
        }
        finally
        {
            foreach (var token in published)
            {
                challenges.Clear(token);
            }
        }
    }

    private async Task<AcmeContext> ContextAsync(CancellationToken ct)
    {
        var directory = AcmeDirectories.Resolve(options.Value.Directory);
        var stored = await accounts.GetAsync(ct);

        // accounts are per CA, a staging key against production fails "account does not exist"
        if (stored is not null && stored.Directory == directory.ToString())
        {
            return new AcmeContext(directory, KeyFactory.FromPem(protector.Unprotect(stored.EncryptedKeyPem)));
        }

        var email = (await settings.GetAsync(ct))?.AcmeEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new CertificateIssuanceException("Set the Let's Encrypt email address in Settings before requesting a certificate.");
        }

        var acme = new AcmeContext(directory);
        await acme.NewAccount(email, termsOfServiceAgreed: true);
        await accounts.SaveAsync(new StoredAcmeAccount
        {
            Directory = directory.ToString(),
            EncryptedKeyPem = protector.Protect(acme.AccountKey.ToPem()),
        }, ct);

        log.LogInformation("Registered an ACME account at {Directory}", directory);
        return acme;
    }

    private async Task AwaitValidationAsync(IAuthorizationContext authorization, CancellationToken ct)
    {
        var deadline = clock.GetUtcNow().AddSeconds(options.Value.ValidationTimeoutSeconds);

        while (true)
        {
            var resource = await authorization.Resource();
            switch (resource.Status)
            {
                case AuthorizationStatus.Valid:
                    return;
                case AuthorizationStatus.Invalid:
                    throw new CertificateIssuanceException(FailureText(resource));
            }

            if (clock.GetUtcNow() >= deadline)
            {
                throw new CertificateIssuanceException(
                    $"'{resource.Identifier.Value}' was not validated within {options.Value.ValidationTimeoutSeconds}s. " +
                    "Check that the hostname resolves to this server and that port 80 is reachable.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), clock, ct);
        }
    }

    private static string FailureText(Authorization authorization)
    {
        var detail = authorization.Challenges?
            .Select(c => c.Error?.Detail)
            .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

        return $"'{authorization.Identifier.Value}' failed validation: {detail ?? "no detail given"}";
    }
}
