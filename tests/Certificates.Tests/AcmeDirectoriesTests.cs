using FifthBox.ServerManager.Integrations.Certificates;

namespace FifthBox.ServerManager.Integrations.Certificates.Tests;

[TestClass]
public class AcmeDirectoriesTests
{
    [TestMethod]
    public void Defaults_to_staging_when_unset()
    {
        Assert.IsFalse(AcmeDirectories.IsProduction(null));
        Assert.IsFalse(AcmeDirectories.IsProduction(""));
        Assert.IsFalse(AcmeDirectories.IsProduction("   "));
    }

    [TestMethod]
    public void Production_is_an_explicit_opt_in()
    {
        Assert.IsTrue(AcmeDirectories.IsProduction("production"));
        Assert.IsTrue(AcmeDirectories.IsProduction("Production"));
        Assert.IsTrue(AcmeDirectories.IsProduction("live"));
    }

    [TestMethod]
    public void An_unrecognised_name_throws_rather_than_guessing()
    {
        // falling back to prod burns the weekly duplicate limit, falling back to staging issues untrusted certs
        Assert.ThrowsExactly<ArgumentException>(() => AcmeDirectories.Resolve("prod"));
        Assert.ThrowsExactly<ArgumentException>(() => AcmeDirectories.Resolve("letsencrypt"));
    }

    [TestMethod]
    public void An_absolute_url_is_used_as_given()
    {
        var uri = AcmeDirectories.Resolve("https://ca.internal/acme/directory");

        Assert.AreEqual("https://ca.internal/acme/directory", uri.ToString());
        Assert.IsFalse(AcmeDirectories.IsProduction("https://ca.internal/acme/directory"));
    }
}
