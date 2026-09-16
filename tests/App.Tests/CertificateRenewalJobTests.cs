using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Routes;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class CertificateRenewalJobTests
{
    private static (CertificateRenewalJob job, Mock<ICertificateService> certificates, Mock<IRouteService> routes) Build(int changed)
    {
        var certificates = new Mock<ICertificateService>();
        certificates.Setup(c => c.IssueDueAsync(It.IsAny<CancellationToken>())).ReturnsAsync(changed);
        var routes = new Mock<IRouteService>();
        return (new CertificateRenewalJob(certificates.Object, routes.Object), certificates, routes);
    }

    [TestMethod]
    public async Task Reapplies_nginx_when_a_certificate_changed()
    {
        var (job, _, routes) = Build(changed: 1);

        await job.RunAsync();

        // a renewed cert the edge isn't holding yet changes nothing for visitors
        routes.Verify(r => r.ApplyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Leaves_nginx_alone_when_nothing_changed()
    {
        var (job, _, routes) = Build(changed: 0);

        await job.RunAsync();

        // runs daily and usually has nothing to do, redeploying anyway bounces every site
        routes.Verify(r => r.ApplyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void Runs_daily()
        => Assert.AreEqual(TimeSpan.FromHours(24), Build(0).job.Interval);
}
