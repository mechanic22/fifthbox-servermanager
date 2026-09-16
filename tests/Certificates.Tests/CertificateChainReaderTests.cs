using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FifthBox.ServerManager.Integrations.Certificates;

namespace FifthBox.ServerManager.Integrations.Certificates.Tests;

[TestClass]
public class CertificateChainReaderTests
{
    private static string Pem(string commonName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        return certificate.ExportCertificatePem();
    }

    private static readonly DateTimeOffset Issued = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Reads_the_validity_window()
    {
        var expiry = Issued.AddDays(90);

        var (notBefore, notAfter) = CertificateChainReader.Validity(Pem("app.example.com", Issued, expiry));

        Assert.AreEqual(Issued, notBefore);
        Assert.AreEqual(expiry, notAfter);
    }

    [TestMethod]
    public void Reads_the_leaf_not_the_intermediate()
    {
        var leafExpiry = Issued.AddDays(90);
        var chain = Pem("app.example.com", Issued, leafExpiry)
            + "\n" + Pem("Example Intermediate CA", Issued, Issued.AddYears(10));

        var (_, notAfter) = CertificateChainReader.Validity(chain);

        // renewal is scheduled off this, the intermediate would push it a decade out and the site dies in three months
        Assert.AreEqual(leafExpiry, notAfter);
    }

    [TestMethod]
    public void An_empty_chain_throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CertificateChainReader.Validity("  "));
    }
}
