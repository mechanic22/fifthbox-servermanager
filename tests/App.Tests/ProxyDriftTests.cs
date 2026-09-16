using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

/// saved routes and certs only reach traffic once the edge redeploys
[TestClass]
public class ProxyDriftTests
{
    private static readonly Workload Web = new() { Id = "w1", Name = "web", Image = "nginx" };

    private sealed class Harness
    {
        public required RouteService Service { get; init; }
        public required List<Route> Routes { get; init; }
        public required InMemoryPlatformSettings Settings { get; init; }
        public required List<CertificateMaterial> Certificates { get; init; }

        public required Mock<IWorkloadBackend> Backend { get; init; }
    }

    private static Harness Build(WorkloadState state = WorkloadState.Running)
    {
        var routeList = new List<Route>();
        var certificates = new List<CertificateMaterial>();

        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => routeList);

        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        var proxy = new Mock<IReverseProxy>();
        // echoes the routes, so changing the route set changes the config like the real one
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Returns((IReadOnlyList<RouteConfig> configs, IReadOnlyList<HostCertificate> certs) =>
                string.Join(";", configs.Select(c => $"{c.Hostname}{c.Path}>{c.UpstreamService}:{c.UpstreamPort}"))
                + "|" + string.Join(";", certs.Select(c => c.Hostname)));

        var certificateService = new Mock<ICertificateService>();
        certificateService.Setup(c => c.InstallableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => certificates);

        var backend = new Mock<IWorkloadBackend>();
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "fbsm-nginx", State = state, RunningReplicas = 1, DesiredReplicas = 1 });

        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns(backend.Object);

        var hasher = new Mock<IBasicAuthHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("$2y$FAKEHASH");

        var settings = new InMemoryPlatformSettings();

        return new Harness
        {
            Service = new RouteService(routes.Object, workloads.Object, proxy.Object, certificateService.Object,
                resolver.Object, hasher.Object, settings,
                Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" }),
                Options.Create(new ReverseProxyOptions()),
                TimeProvider.System),
            Routes = routeList,
            Settings = settings,
            Certificates = certificates,
            Backend = backend,
        };
    }

    private static Route Route(string hostname = "app.example.com", bool enabled = true) => new()
    {
        Id = $"r-{hostname}-{enabled}",
        Hostname = hostname,
        Path = "/",
        Target = RouteTarget.Workload,
        WorkloadId = "w1",
        TargetPort = 80,
        Enabled = enabled,
    };

    [TestMethod]
    public async Task Nothing_to_serve_is_not_pending()
    {
        var harness = Build(WorkloadState.NotDeployed);

        var state = await harness.Service.GetProxyStateAsync();

        Assert.IsFalse(state.PendingChanges, "an empty platform shouldn't nag about an edge nobody needs yet");
        Assert.IsNull(state.LastAppliedAt);
    }

    [TestMethod]
    public async Task A_route_that_has_never_been_applied_is_pending()
    {
        var harness = Build(WorkloadState.NotDeployed);
        harness.Routes.Add(Route());

        var state = await harness.Service.GetProxyStateAsync();

        Assert.IsTrue(state.PendingChanges);
    }

    [TestMethod]
    public async Task Applying_clears_pending_and_records_when()
    {
        var harness = Build();
        harness.Routes.Add(Route());

        await harness.Service.ApplyAsync();
        var state = await harness.Service.GetProxyStateAsync();

        Assert.IsFalse(state.PendingChanges);
        Assert.IsNotNull(state.LastAppliedAt);
    }

    [TestMethod]
    public async Task Disabling_a_route_after_applying_is_pending_again()
    {
        // the case the UI got wrong: row greys out but the site keeps being served
        var harness = Build();
        harness.Routes.Add(Route());
        await harness.Service.ApplyAsync();

        harness.Routes[0].Enabled = false;
        var state = await harness.Service.GetProxyStateAsync();

        Assert.IsTrue(state.PendingChanges);
    }

    [TestMethod]
    public async Task A_renewed_certificate_is_pending_even_though_the_config_is_identical()
    {
        // config references certs by path, so hashing it alone would miss a renewal
        var harness = Build();
        harness.Routes.Add(Route());
        harness.Certificates.Add(new CertificateMaterial
        {
            Hostname = "app.example.com",
            PemChain = "-----BEGIN CERTIFICATE-----old",
            PrivateKeyPem = "-----BEGIN EC PRIVATE KEY-----old",
        });
        await harness.Service.ApplyAsync();

        Assert.IsFalse((await harness.Service.GetProxyStateAsync()).PendingChanges);

        harness.Certificates[0] = new CertificateMaterial
        {
            Hostname = "app.example.com",
            PemChain = "-----BEGIN CERTIFICATE-----renewed",
            PrivateKeyPem = "-----BEGIN EC PRIVATE KEY-----renewed",
        };

        Assert.IsTrue((await harness.Service.GetProxyStateAsync()).PendingChanges);
    }

    [TestMethod]
    public async Task A_failed_deploy_leaves_it_pending()
    {
        var harness = Build();
        harness.Routes.Add(Route());
        harness.Backend
            .Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("swarm said no"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => harness.Service.ApplyAsync());

        Assert.AreEqual(0, harness.Settings.SaveCount, "a fingerprint written before the deploy lands would hide real drift");
        Assert.IsTrue((await harness.Service.GetProxyStateAsync()).PendingChanges);
    }
}
