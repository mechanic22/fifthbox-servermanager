using System.Text;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificateChainPem
{
    /// leaf then issuers, no root. Certes' ToPem() only knows production roots
    /// and throws on staging certs
    public static string Combine(string leaf, IEnumerable<string> issuers)
    {
        if (string.IsNullOrWhiteSpace(leaf))
        {
            throw new ArgumentException("The leaf certificate is empty.", nameof(leaf));
        }

        var pem = new StringBuilder();
        pem.Append(leaf.Trim()).Append('\n');

        foreach (var issuer in issuers.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            pem.Append(issuer.Trim()).Append('\n');
        }

        return pem.ToString();
    }
}
