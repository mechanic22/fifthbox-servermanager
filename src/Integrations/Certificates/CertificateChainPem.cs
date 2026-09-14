using System.Text;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificateChainPem
{
    /// Leaf first, then the issuers the CA sent, in order. Certes' own ToPem() rebuilds the chain from
    /// a bundled issuer store that only carries the production roots, so anything issued by the staging
    /// CA throws "can not find issuer" — and the root it would append isn't wanted here anyway: a
    /// server sends leaf + intermediates and lets the client supply the root.
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
