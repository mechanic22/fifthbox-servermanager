using FifthBox.ServerManager.App.Certificates;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class IssuableHostnameTests
{
    [TestMethod]
    [DataRow("app.example.com")]
    [DataRow("deep.sub.example.co.uk")]
    [DataRow("APP.Example.Com")]
    [DataRow("app.example.com.")]
    public void A_public_hostname_is_issuable(string hostname)
    {
        Assert.IsNull(IssuableHostname.BlockedReason(hostname));
    }

    [TestMethod]
    [DataRow("dev-01.local")]
    [DataRow("box.localhost")]
    [DataRow("a.b.internal")]
    [DataRow("thing.test")]
    [DataRow("host.home.arpa")]
    public void A_reserved_suffix_is_blocked(string hostname)
    {
        StringAssert.Contains(IssuableHostname.BlockedReason(hostname), "reserved suffix");
    }

    [TestMethod]
    [DataRow("mylocal.com")]
    [DataRow("local.example.com")]
    [DataRow("testing.example.com")]
    public void A_suffix_only_matches_on_a_label_boundary(string hostname)
    {
        Assert.IsNull(IssuableHostname.BlockedReason(hostname), "'local' inside a label isn't the .local TLD");
    }

    [TestMethod]
    public void A_single_label_name_is_blocked()
    {
        StringAssert.Contains(IssuableHostname.BlockedReason("dev-01"), "no domain");
    }

    [TestMethod]
    [DataRow("10.0.0.5")]
    [DataRow("2001:db8::1")]
    public void An_ip_literal_is_blocked(string hostname)
    {
        StringAssert.Contains(IssuableHostname.BlockedReason(hostname), "not IP addresses");
    }

    [TestMethod]
    public void A_custom_suffix_list_is_used_as_given()
    {
        string[] configured = ["lan", ".corp"];

        StringAssert.Contains(IssuableHostname.BlockedReason("nas.lan", configured), "'.lan'");
        StringAssert.Contains(IssuableHostname.BlockedReason("app.corp", configured), "'.corp'",
            "a leading dot in config is tolerated");
    }
}
