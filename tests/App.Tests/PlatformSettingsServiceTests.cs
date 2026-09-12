using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using FifthBox.ServerManager.Shared.Routes;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class PlatformSettingsServiceTests
{
    private static (PlatformSettingsService svc, Mock<IPlatformSettingsRepository> repo, Mock<IRouteService> routes) Build(
        PlatformSettings? existing = null)
    {
        var repo = new Mock<IPlatformSettingsRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repo.Setup(r => r.SaveAsync(It.IsAny<PlatformSettings>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteResponse { Id = "r1", Hostname = "x", Path = "/" });

        var svc = new PlatformSettingsService(
            repo.Object,
            routes.Object,
            Options.Create(new ReverseProxyOptions()),
            Options.Create(new HostDeploymentOptions()),
            TimeProvider.System);

        return (svc, repo, routes);
    }

    [TestMethod]
    public async Task Update_normalizes_and_persists()
    {
        var (svc, repo, _) = Build();
        PlatformSettings? saved = null;
        repo.Setup(r => r.SaveAsync(It.IsAny<PlatformSettings>(), It.IsAny<CancellationToken>()))
            .Callback<PlatformSettings, CancellationToken>((s, _) => saved = s).Returns(Task.CompletedTask);

        var result = await svc.UpdateAsync(new UpdatePlatformSettingsRequest
        {
            RootDomain = "  Example.COM ", AcmeEmail = "root@example.com",
        });

        Assert.AreEqual("example.com", saved!.RootDomain);
        Assert.AreEqual("root@example.com", saved.AcmeEmail);
        Assert.AreEqual("example.com", result.RootDomain);
    }

    [TestMethod]
    public async Task Update_with_invalid_email_throws_validation()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.UpdateAsync(new UpdatePlatformSettingsRequest { AcmeEmail = "not-an-email" }));
    }

    [TestMethod]
    public async Task Get_returns_empty_defaults_when_unset()
    {
        var (svc, _, _) = Build();
        var result = await svc.GetAsync();

        Assert.IsNull(result.RootDomain);
        Assert.IsNull(result.AcmeEmail);
    }

    [TestMethod]
    public async Task Setting_a_root_domain_gives_the_manager_its_own_route()
    {
        var (svc, _, routes) = Build();
        CreateRouteRequest? created = null;
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateRouteRequest, CancellationToken>((r, _) => created = r)
            .ReturnsAsync(new RouteResponse { Id = "r1", Hostname = "x", Path = "/" });

        await svc.UpdateAsync(new UpdatePlatformSettingsRequest { RootDomain = "fbsm.example.com", ManagerPrefix = "manage" });

        Assert.AreEqual("manage.fbsm.example.com", created!.Hostname);
        Assert.AreEqual(RouteTarget.External, created.Target);
        Assert.AreEqual("fbsm-host", created.UpstreamHost, "nginx resolves the Host by service name over the overlay");
        Assert.AreEqual(8080, created.TargetPort);
        Assert.IsTrue(created.WebSockets, "the SignalR hubs go quiet without the upgrade headers");
    }

    [TestMethod]
    public async Task Saving_the_same_settings_again_does_not_re_add_the_route()
    {
        var existing = new PlatformSettings { RootDomain = "fbsm.example.com", ManagerPrefix = "manage" };
        var (svc, _, routes) = Build(existing);

        await svc.UpdateAsync(new UpdatePlatformSettingsRequest
        {
            RootDomain = "fbsm.example.com", ManagerPrefix = "manage", AcmeEmail = "root@example.com",
        });

        routes.Verify(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Changing_the_prefix_provisions_the_new_address_and_leaves_the_old_route_alone()
    {
        var existing = new PlatformSettings { RootDomain = "fbsm.example.com", ManagerPrefix = "manage" };
        var (svc, _, routes) = Build(existing);
        CreateRouteRequest? created = null;
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateRouteRequest, CancellationToken>((r, _) => created = r)
            .ReturnsAsync(new RouteResponse { Id = "r2", Hostname = "x", Path = "/" });

        await svc.UpdateAsync(new UpdatePlatformSettingsRequest { RootDomain = "fbsm.example.com", ManagerPrefix = "admin" });

        // Deleting the old one here would take its certificate with it, from a settings save.
        Assert.AreEqual("admin.fbsm.example.com", created!.Hostname);
        routes.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_blank_prefix_means_no_manager_route()
    {
        var (svc, _, routes) = Build();

        await svc.UpdateAsync(new UpdatePlatformSettingsRequest { RootDomain = "fbsm.example.com", ManagerPrefix = "  " });

        routes.Verify(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_dotted_prefix_is_rejected()
    {
        var (svc, _, _) = Build();

        // Two labels fall outside a *.<root domain> wildcard — its own DNS record and its own
        // certificate, which is not what picking a subdomain here implies.
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.UpdateAsync(new UpdatePlatformSettingsRequest { RootDomain = "fbsm.example.com", ManagerPrefix = "admin.internal" }));
    }

    [TestMethod]
    public async Task A_taken_hostname_does_not_fail_the_save()
    {
        var (svc, repo, routes) = Build();
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("taken"));

        var result = await svc.UpdateAsync(new UpdatePlatformSettingsRequest { RootDomain = "fbsm.example.com", ManagerPrefix = "manage" });

        Assert.AreEqual("fbsm.example.com", result.RootDomain);
        repo.Verify(r => r.SaveAsync(It.IsAny<PlatformSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
