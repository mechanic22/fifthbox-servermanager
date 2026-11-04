using FifthBox.ServerManager.Client.Web.Components;
using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class RouteUrlTests
{
    [TestMethod]
    public void A_valid_cert_goes_over_https() =>
        Assert.AreEqual("https://app.example.com/", RouteUrl.For("app.example.com", "/", CertificateStatus.Valid, 80, 443));

    [TestMethod]
    [DataRow(CertificateStatus.Pending)]
    [DataRow(CertificateStatus.Failed)]
    [DataRow(null)]
    public void Anything_else_goes_over_http(CertificateStatus? tls) =>
        Assert.AreEqual("http://app.example.com/", RouteUrl.For("app.example.com", "/", tls, 80, 443));

    [TestMethod]
    public void The_path_stays_on_the_end() =>
        Assert.AreEqual("https://app.example.com/api", RouteUrl.For("app.example.com", "/api", CertificateStatus.Valid, 80, 443));

    [TestMethod]
    public void A_moved_edge_port_shows_on_the_scheme_it_belongs_to()
    {
        Assert.AreEqual("https://app.example.com:8443/", RouteUrl.For("app.example.com", "/", CertificateStatus.Valid, 8080, 8443));
        Assert.AreEqual("http://app.example.com:8080/", RouteUrl.For("app.example.com", "/", null, 8080, 8443));
    }
}
