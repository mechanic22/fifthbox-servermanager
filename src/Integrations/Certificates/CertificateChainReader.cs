using System.Security.Cryptography.X509Certificates;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificateChainReader
{
    /// The validity window of the leaf — the first certificate in the PEM chain. Renewal is scheduled
    /// off this, so reading the wrong element (an intermediate outlives the leaf by years) would mean
    /// never renewing until the site was already broken.
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
