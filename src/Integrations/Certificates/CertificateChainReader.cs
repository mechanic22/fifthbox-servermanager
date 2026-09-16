using System.Security.Cryptography.X509Certificates;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificateChainReader
{
    /// the leaf (first cert). an intermediate outlives it by years and we'd never renew
    public static (DateTimeOffset NotBefore, DateTimeOffset NotAfter) Validity(string pemChain)
    {
        if (string.IsNullOrWhiteSpace(pemChain))
        {
            throw new ArgumentException("The certificate chain is empty.", nameof(pemChain));
        }

        using var leaf = X509Certificate2.CreateFromPem(pemChain);
        return (leaf.NotBefore.ToUniversalTime(), leaf.NotAfter.ToUniversalTime());
    }
}
