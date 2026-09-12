using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class RouteServiceTests
{
    private static readonly Workload Web = new() { Id = "w1", Name = "web", Image = "nginx" };

    private static RouteService NewService(
        Mock<IRouteRepository> routes,
        Mock<IWorkloadRepository> workloads,
        Mock<IReverseProxy> proxy,
        Mock<IWorkloadBackend>? backend = null,
        Mock<ICertificateService>? certificates = null,
        ReverseProxyOptions? proxyOptions = null,
        InMemoryPlatformSettings? settings = null)
    {
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns((backend ?? new Mock<IWorkloadBackend>()).Object);
        var hasher = new Mock<IBasicAuthHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("$2y$FAKEHASH");
        var certs = certificates ?? WithCertificates();
        return new(routes.Object, workloads.Object, proxy.Object,
            certs.Object,
            resolver.Object,
            hasher.Object,
            settings ?? new InMemoryPlatformSettings(),
            Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" }),
            Options.Create(proxyOptions ?? new ReverseProxyOptions()),
            TimeProvider.System);
    }

    private static Mock<ICertificateService> WithCertificates(params CertificateMaterial[] installable)
    {
        var certificates = new Mock<ICertificateService>();
        certificates.Setup(c => c.InstallableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(installable);
        return certificates;
    }

    private static CertificateMaterial Material(string hostname) => new()
    {
        Hostname = hostname,
        PemChain = "-----BEGIN CERTIFICATE-----leaf",
        PrivateKeyPem = "-----BEGIN EC PRIVATE KEY-----secret",
    };

    private static (RouteService svc, Mock<IRouteRepository> routes) Build(bool workloadExists = true)
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        routes.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workloadExists ? Web : null);
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>())).Returns("cfg");

        return (NewService(routes, workloads, proxy), routes);
    }

    [TestMethod]
    public async Task Create_normalizes_hostname_and_resolves_workload_name()
    {
        var (svc, routes) = Build();
        Route? saved = null;
        routes.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()))
            .Callback<Route, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        var result = await svc.CreateAsync(new CreateRouteRequest { Hostname = "App.Example.COM", Path = "/", WorkloadId = "w1", TargetPort = 80 });

        Assert.AreEqual("app.example.com", result.Hostname);
        Assert.AreEqual("web", result.WorkloadName);
        Assert.AreEqual("app.example.com", saved!.Hostname);
    }

    [TestMethod]
    public async Task Create_defaults_path_when_blank()
    {
        var (svc, _) = Build();
        var result = await svc.CreateAsync(new CreateRouteRequest { Hostname = "a.com", Path = "", WorkloadId = "w1", TargetPort = 80 });
        Assert.AreEqual("/", result.Path);
    }

    [TestMethod]
    public async Task Create_with_missing_workload_throws_validation()
    {
        var (svc, _) = Build(workloadExists: false);
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateRouteRequest { Hostname = "a.com", WorkloadId = "w1", TargetPort = 80 }));
    }

    [TestMethod]
    public async Task Create_with_blank_hostname_throws_validation()
    {
        var (svc, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateRouteRequest { Hostname = "  ", WorkloadId = "w1", TargetPort = 80 }));
    }

    [TestMethod]
    public async Task Create_with_bad_port_throws_validation()
    {
        var (svc, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.CreateAsync(new CreateRouteRequest { Hostname = "a.com", WorkloadId = "w1", TargetPort = 0 }));
    }

    [TestMethod]
    public async Task Create_duplicate_host_path_throws_conflict()
    {
        var (svc, routes) = Build();
        routes.Setup(r => r.ExistsAsync("a.com", "/", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateAsync(new CreateRouteRequest { Hostname = "a.com", Path = "/", WorkloadId = "w1", TargetPort = 80 }));
    }

    [TestMethod]
    public async Task RenderConfig_resolves_upstream_service_and_port()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new() { Id = "r1", Hostname = "a.com", Path = "/", WorkloadId = "w1", TargetPort = 80 },
        });
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        var proxy = new Mock<IReverseProxy>();
        IReadOnlyList<RouteConfig>? captured = null;
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((c, _) => captured = c).Returns("cfg");

        var svc = NewService(routes, workloads, proxy);
        var result = await svc.RenderConfigAsync();

        Assert.AreEqual("cfg", result);
        Assert.AreEqual("fbsm--web", captured!.Single().UpstreamService);
        Assert.AreEqual(80, captured!.Single().UpstreamPort);
    }

    [TestMethod]
    public async Task A_workload_route_proxies_to_the_namespaced_service()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new() { Id = "r1", Hostname = "a.com", Path = "/", WorkloadId = "w1", TargetPort = 80 },
        });
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });
        var proxy = new Mock<IReverseProxy>();
        IReadOnlyList<RouteConfig>? captured = null;
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((c, _) => captured = c).Returns("cfg");

        await NewService(routes, workloads, proxy).RenderConfigAsync();

        // Must track whatever the swarm backend actually named the service, or nginx proxies to nothing.
        Assert.AreEqual("fbsm--web", captured!.Single().UpstreamService);
    }

    [TestMethod]
    public async Task Apply_mounts_each_certificate_as_a_pair_of_swarm_secrets()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new() { Id = "r1", Hostname = "app.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80 },
        });
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });
        var proxy = new Mock<IReverseProxy>();
        IReadOnlyList<HostCertificate>? rendered = null;
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((_, c) => rendered = c).Returns("cfg");

        var backend = new Mock<IWorkloadBackend>();
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await NewService(routes, workloads, proxy, backend, WithCertificates(Material("app.example.com"))).ApplyAsync();

        Assert.HasCount(2, sent!.Secrets);
        var chain = sent.Secrets.Single(s => s.Name == "cert-app.example.com");
        var key = sent.Secrets.Single(s => s.Name == "key-app.example.com");
        Assert.AreEqual("cert-app.example.com.pem", chain.FileName);
        Assert.AreEqual("-----BEGIN EC PRIVATE KEY-----secret", key.Content);

        // The paths handed to the generator have to be where those files actually land, or nginx points
        // ssl_certificate at nothing and refuses to start.
        var certified = rendered!.Single();
        Assert.AreEqual($"/run/secrets/{chain.FileName}", certified.CertificatePath);
        Assert.AreEqual($"/run/secrets/{key.FileName}", certified.PrivateKeyPath);
    }

    [TestMethod]
    public async Task Apply_without_certificates_mounts_no_secrets()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>());
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload>());
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>())).Returns("cfg");
        var backend = new Mock<IWorkloadBackend>();
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await NewService(routes, workloads, proxy, backend).ApplyAsync();

        Assert.IsEmpty(sent!.Secrets);
    }

    [TestMethod]
    public async Task Apply_publishes_the_configured_edge_ports()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>());
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload>());
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>())).Returns("cfg");
        var backend = new Mock<IWorkloadBackend>();
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await NewService(routes, workloads, proxy, backend,
            proxyOptions: new ReverseProxyOptions { HttpPort = 8080, HttpsPort = 8443 }).ApplyAsync();

        // Published moves; the container still listens on 80/443, so the generated config is unaffected.
        var http = sent!.Ports.Single(p => p.Target == 80);
        var https = sent.Ports.Single(p => p.Target == 443);
        Assert.AreEqual(8080, http.Published);
        Assert.AreEqual(8443, https.Published);
        Assert.IsTrue(sent.Ports.All(p => p.Mode == PortPublishMode.Host));
    }

    [TestMethod]
    public async Task Apply_deploys_nginx_pinned_with_host_ports_and_config()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>());
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload>());
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>())).Returns("nginx-config-text");

        var backend = new Mock<IWorkloadBackend>();
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "fbsm-nginx", Deployed = true, DesiredReplicas = 1, RunningReplicas = 1, State = WorkloadState.Running });

        var status = await NewService(routes, workloads, proxy, backend).ApplyAsync();

        Assert.AreEqual("fbsm-nginx", sent!.Name);
        Assert.AreEqual("nginx:1.27", sent.Image);
        Assert.IsTrue(sent.PinToControlNode);
        Assert.AreEqual("fbsm-overlay", sent.Network);
        Assert.HasCount(2, sent.Ports);
        Assert.IsTrue(sent.Ports.All(p => p.Mode == PortPublishMode.Host));
        Assert.HasCount(1, sent.Configs);
        Assert.AreEqual("nginx-config-text", sent.Configs[0].Content);
        Assert.AreEqual("/etc/nginx/conf.d/default.conf", sent.Configs[0].Path);
        Assert.AreEqual(WorkloadState.Running, status.State);
    }

    [TestMethod]
    public async Task Create_with_basic_auth_hashes_the_password_and_never_returns_it()
    {
        var (svc, routes) = Build();
        Route? saved = null;
        routes.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()))
            .Callback<Route, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        var result = await svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "secure.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80,
            BasicAuthEnabled = true, BasicAuthUsername = "admin", BasicAuthPassword = "s3cret",
        });

        Assert.IsTrue(saved!.BasicAuthEnabled);
        Assert.AreEqual("admin", saved.BasicAuthUsername);
        Assert.AreEqual("$2y$FAKEHASH", saved.BasicAuthPasswordHash);   // the hasher output, not the plaintext
        Assert.DoesNotContain("s3cret", saved.BasicAuthPasswordHash!);
        Assert.IsTrue(result.BasicAuthEnabled);
        Assert.AreEqual("admin", result.BasicAuthUsername);
        // RouteResponse has no password/hash member — nothing to leak.
    }

    [TestMethod]
    public async Task Enable_basic_auth_without_a_password_throws_validation()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "secure.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80,
            BasicAuthEnabled = true, BasicAuthUsername = "admin",
        }));
    }

    [TestMethod]
    public async Task Websockets_flag_is_persisted()
    {
        var (svc, routes) = Build();
        Route? saved = null;
        routes.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()))
            .Callback<Route, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        await svc.CreateAsync(new CreateRouteRequest { Hostname = "ws.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80, WebSockets = true });

        Assert.IsTrue(saved!.WebSockets);
    }

    private static readonly Workload NativeGameServer = new()
    {
        Id = "n1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "/srv/srcds",
    };

    [TestMethod]
    public async Task Create_route_to_a_native_workload_throws_validation()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.FindByIdAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(NativeGameServer);
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { NativeGameServer });
        var proxy = new Mock<IReverseProxy>();

        var svc = NewService(routes, workloads, proxy);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(
            new CreateRouteRequest { Hostname = "game.example.com", Path = "/", WorkloadId = "n1", TargetPort = 27015 }));
        routes.Verify(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Render_skips_routes_pointing_at_a_native_workload()
    {
        // nginx resolves upstreams at config load; one unresolvable name stops it starting at all, so a
        // stray route must never reach the generated config.
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new() { Id = "r1", Hostname = "app.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80 },
            new() { Id = "r2", Hostname = "game.example.com", Path = "/", WorkloadId = "n1", TargetPort = 27015 },
        });
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web, NativeGameServer });

        IReadOnlyList<RouteConfig>? captured = null;
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((c, _) => captured = c).Returns("cfg");

        await NewService(routes, workloads, proxy).RenderConfigAsync();

        Assert.AreEqual("fbsm--web", captured!.Single().UpstreamService);
    }

    [TestMethod]
    public async Task A_disabled_route_is_kept_but_left_out_of_the_generated_config()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new() { Id = "r1", Hostname = "live.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80, Enabled = true },
            new() { Id = "r2", Hostname = "off.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80, Enabled = false },
        });
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        IReadOnlyList<RouteConfig>? captured = null;
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((c, _) => captured = c).Returns("cfg");

        var svc = NewService(routes, workloads, proxy);

        await svc.RenderConfigAsync();
        Assert.AreEqual("live.example.com", captured!.Single().Hostname);

        // Still listed — disabling hides it from nginx, it doesn't delete it.
        Assert.HasCount(2, await svc.ListAsync());
    }

    [TestMethod]
    public async Task SetEnabled_flips_the_flag_and_stamps_updated()
    {
        var route = new Route
        {
            Id = "r1", Hostname = "app.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80,
            Enabled = true, UpdatedAt = DateTimeOffset.UnixEpoch,
        };
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(route);
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        var result = await NewService(routes, workloads, new Mock<IReverseProxy>()).SetEnabledAsync("r1", false);

        Assert.IsFalse(result.Enabled);
        Assert.IsFalse(route.Enabled);
        Assert.IsGreaterThan(DateTimeOffset.UnixEpoch, route.UpdatedAt);
        routes.Verify(r => r.UpdateAsync(route, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Updating_a_route_leaves_its_enabled_state_alone()
    {
        var route = new Route
        {
            Id = "r1", Hostname = "app.example.com", Path = "/", WorkloadId = "w1", TargetPort = 80, Enabled = false,
        };
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(route);
        routes.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(Web);
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload> { Web });

        var result = await NewService(routes, workloads, new Mock<IReverseProxy>()).UpdateAsync("r1",
            new UpdateRouteRequest { Hostname = "app.example.com", Path = "/", WorkloadId = "w1", TargetPort = 8080 });

        Assert.IsFalse(result.Enabled, "an edit must not silently bring a disabled route back online");
    }

    [TestMethod]
    public async Task External_route_needs_no_workload_and_defers_resolution()
    {
        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Route>
        {
            new()
            {
                Id = "r1", Hostname = "nas.example.com", Path = "/", Target = RouteTarget.External,
                UpstreamHost = "nas.lan", UpstreamScheme = UpstreamScheme.Https, TargetPort = 5001, Enabled = true,
            },
        });
        // No workloads at all — an external route must survive the routable gate regardless.
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload>());

        IReadOnlyList<RouteConfig>? captured = null;
        var proxy = new Mock<IReverseProxy>();
        proxy.Setup(p => p.Render(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<HostCertificate>>()))
            .Callback<IReadOnlyList<RouteConfig>, IReadOnlyList<HostCertificate>>((c, _) => captured = c).Returns("cfg");

        await NewService(routes, workloads, proxy).RenderConfigAsync();

        var config = captured!.Single();
        Assert.AreEqual("nas.lan", config.UpstreamService);
        Assert.AreEqual(UpstreamScheme.Https, config.Scheme);
    }

    [TestMethod]
    public async Task Creating_an_external_route_without_a_host_throws_validation()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "nas.example.com", Path = "/", Target = RouteTarget.External, TargetPort = 5000,
        }));
    }

    [TestMethod]
    [DataRow("http://nas.lan")]
    [DataRow("nas.lan:5000")]
    [DataRow("nas.lan/admin")]
    [DataRow("nas.lan; return 200")]
    public async Task An_upstream_host_that_is_not_a_bare_host_throws_validation(string host)
    {
        var (svc, _) = Build();

        // It is interpolated straight into proxy_pass, so anything carrying a scheme, port, path or a
        // second directive is refused rather than escaped.
        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "nas.example.com", Path = "/", Target = RouteTarget.External,
            UpstreamHost = host, TargetPort = 5000,
        }));
    }

    [TestMethod]
    public async Task Creating_a_workload_route_without_a_workload_throws_validation()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "app.example.com", Path = "/", Target = RouteTarget.Workload, TargetPort = 80,
        }));
    }

    [TestMethod]
    public async Task An_external_route_keeps_no_workload_id()
    {
        var (svc, repo) = Build();
        Route? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Route>(), It.IsAny<CancellationToken>()))
            .Callback<Route, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        await svc.CreateAsync(new CreateRouteRequest
        {
            Hostname = "nas.example.com", Path = "/", Target = RouteTarget.External,
            UpstreamHost = "nas.lan", WorkloadId = "w1", TargetPort = 5000,
        });

        Assert.IsNull(saved!.WorkloadId, "a stray workload id would make the route ambiguous");
        Assert.AreEqual("nas.lan", saved.UpstreamHost);
    }
}
