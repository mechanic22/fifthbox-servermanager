using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class DefaultHostnameTests
{
    [TestMethod]
    public void Joins_the_workload_name_to_the_root_domain()
        => Assert.AreEqual("web.apps.example.com", DefaultHostname.For("web", "apps.example.com"));

    [TestMethod]
    public void Normalizes_case_and_a_stray_leading_dot()
    {
        Assert.AreEqual("web.example.com", DefaultHostname.For("Web", ".Example.COM"));
        Assert.AreEqual("web.example.com", DefaultHostname.For("web", "example.com."));
    }

    [TestMethod]
    public void Is_null_without_a_root_domain()
    {
        Assert.IsNull(DefaultHostname.For("web", null));
        Assert.IsNull(DefaultHostname.For("web", "   "));
    }
}
