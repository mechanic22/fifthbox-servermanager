using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FifthBox.ServerManager.Integrations.Certificates;

namespace FifthBox.ServerManager.Integrations.Certificates.Tests;

[TestClass]
public class CertificateChainPemTests
{
    private static string Pem(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
        return certificate.ExportCertificatePem();
    }

    [TestMethod]
    public void Puts_the_leaf_first()
    {
        var leaf = Pem("app.example.com");

        var chain = CertificateChainPem.Combine(leaf, [Pem("Intermediate"), Pem("Root")]);

        using var parsed = X509Certificate2.CreateFromPem(chain);
        Assert.AreEqual("CN=app.example.com", parsed.Subject);
    }

    [TestMethod]
    public void Keeps_every_issuer_in_order()
    {
        var chain = CertificateChainPem.Combine(Pem("app.example.com"), [Pem("Intermediate"), Pem("Root")]);

        var certificates = new X509Certificate2Collection();
        certificates.ImportFromPem(chain);

        CollectionAssert.AreEqual(
            new[] { "CN=app.example.com", "CN=Intermediate", "CN=Root" },
            certificates.Select(c => c.Subject).ToArray());
    }

    [TestMethod]
    public void Survives_a_chain_the_ca_sent_without_issuers()
    {
        var chain = CertificateChainPem.Combine(Pem("app.example.com"), []);

        var certificates = new X509Certificate2Collection();
        certificates.ImportFromPem(chain);

        Assert.HasCount(1, certificates);
    }

    [TestMethod]
    public void Rejects_an_empty_leaf()
        => Assert.ThrowsExactly<ArgumentException>(() => CertificateChainPem.Combine("  ", []));
}
