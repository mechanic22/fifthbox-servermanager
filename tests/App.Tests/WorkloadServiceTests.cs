using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadServiceTests
{
    private static readonly Caller Admin = new("admin", IsAdmin: true);

    private static WorkloadService Build(
        Mock<IWorkloadRepository> repo,
        Mock<IWorkloadBackend>? backend = null,
        Mock<IAgentRepository>? agents = null,
        Mock<IWorkloadGroupRepository>? groups = null,
        Mock<INodeService>? nodes = null,
        Mock<IDeployedSpecSource>? deployedSpecs = null,
        Mock<IAgentRegistry>? connections = null,
        Mock<IRouteService>? routes = null,
        string? rootDomain = null,
        ClusterState? state = null,
        IReadOnlyList<AccessGrant>? grants = null)
    {
        var agentRepo = agents ?? new Mock<IAgentRepository>();
        agentRepo.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent>());
        var groupRepo = groups ?? new Mock<IWorkloadGroupRepository>();
        groupRepo.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<WorkloadGroup>());
        var backendMock = backend ?? new Mock<IWorkloadBackend>();
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns(backendMock.Object);
        var nodeService = nodes ?? new Mock<INodeService>();
        if (nodes is null)
        {
            nodeService.Setup(n => n.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NodeResponse>());
        }
        var registry = connections ?? Online();
        var settings = new Mock<IPlatformSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformSettings { RootDomain = rootDomain });
        var grantRepo = GrantRepo(grants);
        var specs = deployedSpecs ?? new Mock<IDeployedSpecSource>();
        var protector = new ReversibleProtector();
        var cluster = Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" });
        var access = new WorkloadAccess(grantRepo.Object, groupRepo.Object, TeamRepo.None());
        var loader = new WorkloadLoader(repo.Object, access);
        var factory = new WorkloadDeploymentFactory(protector, cluster);
        var lifecycle = new WorkloadLifecycleService(
            loader, repo.Object, access, resolver.Object, specs.Object,
            state ?? new ClusterState(), factory, TimeProvider.System);

        return new WorkloadService(
            repo.Object,
            loader,
            lifecycle,
            agentRepo.Object,
            registry.Object,
            groupRepo.Object,
            access,
            grantRepo.Object,
            resolver.Object,
            (routes ?? RouteService()).Object,
            settings.Object,
            nodeService.Object,
            protector,
            factory,
            TimeProvider.System);
    }

    /// The grant store the real WorkloadAccess resolves against — so a non-admin Caller in these tests
    /// goes through the same resolution the Host does.
    private static Mock<IAccessGrantRepository> GrantRepo(IReadOnlyList<AccessGrant>? grants)
    {
        var repo = new Mock<IAccessGrantRepository>();
        repo.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grants?.ToList() ?? []);
        repo.Setup(g => g.RemoveForTargetAsync(It.IsAny<AccessScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    /// Stands in for AES without a key: reversible, and — like the real thing — a fresh call on the same
    /// plaintext produces different ciphertext, which is what the pending-changes tests rely on.
    private sealed class ReversibleProtector : ISecretProtector
    {
        private int _nonce;

        public string Protect(string plaintext) => $"enc{Interlocked.Increment(ref _nonce)}:{plaintext}";

        public string Unprotect(string ciphertext) => ciphertext[(ciphertext.IndexOf(':') + 1)..];
    }

    [TestMethod]
    public async Task An_http_container_gets_a_route_at_its_name_under_the_root_domain()
    {
        var routes = RouteService();
        CreateRouteRequest? created = null;
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateRouteRequest, CancellationToken>((r, _) => created = r)
            .ReturnsAsync(new RouteResponse { Id = "r1", Hostname = "x", Path = "/" });

        await Build(Repo(), routes: routes, rootDomain: "apps.example.com")
            .CreateAsync(Admin, new CreateWorkloadRequest { Name = "My App", Image = "nginx", HttpPort = 8080 });

        Assert.AreEqual("my-app.apps.example.com", created!.Hostname);
        Assert.AreEqual("/", created.Path);
        Assert.AreEqual(8080, created.TargetPort);
        Assert.AreEqual(RouteTarget.Workload, created.Target);
    }

    [TestMethod]
    public async Task A_container_without_an_http_port_gets_no_route()
    {
        var routes = RouteService();

        await Build(Repo(), routes: routes, rootDomain: "apps.example.com")
            .CreateAsync(Admin, new CreateWorkloadRequest { Name = "worker", Image = "worker:1" });

        // Plenty of containers aren't web apps. Naming the port is how you say "this one is".
        routes.Verify(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task No_root_domain_means_no_automatic_address()
    {
        var routes = RouteService();

        await Build(Repo(), routes: routes, rootDomain: null)
            .CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx", HttpPort = 80 });

        routes.Verify(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_native_workload_never_gets_one()
    {
        var routes = RouteService();

        await Build(Repo(), routes: routes, agents: WithAgent(), rootDomain: "apps.example.com")
            .CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
            });

        // nginx reaches containers by service name over the overlay; an agent process is host-bound and
        // isn't on it at all.
        routes.Verify(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_taken_hostname_does_not_fail_the_workload()
    {
        var routes = RouteService();
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("taken"));

        var result = await Build(Repo(), routes: routes, rootDomain: "apps.example.com")
            .CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx", HttpPort = 80 });

        // The address is a convenience. Losing it must not lose the workload the operator just defined.
        Assert.AreEqual("web", result.Name);
    }

    [TestMethod]
    public async Task A_container_deploys_under_a_namespaced_service_name()
    {
        var workload = new Workload { Id = "w1", Name = "grafana", Kind = WorkloadKind.Container, Image = "grafana/grafana" };
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await Build(repo, backend).DeployAsync(Admin, "w1");

        // Deploy updates whatever service already answers to this name, so an unnamespaced one could
        // take over something else on a shared daemon.
        Assert.AreEqual("fbsm--grafana", sent!.Name);
    }

    [TestMethod]
    public async Task A_native_workload_keeps_its_plain_name()
    {
        var workload = NativeOn("a1", deployed: false);
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await Build(repo, backend, agents: WithAgent()).DeployAsync(Admin, "w1");

        // The agent tracks running processes by this name; renaming would orphan whatever is up.
        Assert.AreEqual("srcds", sent!.Name);
    }

    private static Mock<IRouteService> RouteService()
    {
        var routes = new Mock<IRouteService>();
        routes.Setup(r => r.CreateAsync(It.IsAny<CreateRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteResponse { Id = "r1", Hostname = "x", Path = "/" });
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return routes;
    }

    /// Agent presence. With no ids everything is online; naming ids leaves every other agent offline.
    private static Mock<IAgentRegistry> Online(params string[] agentIds)
    {
        var registry = new Mock<IAgentRegistry>();
        registry.Setup(r => r.IsOnline(It.IsAny<string>()))
            .Returns<string>(id => agentIds.Length == 0 || agentIds.Contains(id));
        return registry;
    }

    private static Mock<IWorkloadRepository> Repo()
    {
        var repo = new Mock<IWorkloadRepository>();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repo;
    }

    [TestMethod]
    public async Task Create_slugifies_name_and_persists()
    {
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w)
            .Returns(Task.CompletedTask);

        var result = await Build(repo).CreateAsync(Admin, new CreateWorkloadRequest { Name = "My App!", Image = "nginx:1.27" });

        Assert.AreEqual("my-app", result.Name);
        Assert.AreEqual("my-app", saved!.Name);
        repo.Verify(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Create_with_blank_name_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo()).CreateAsync(Admin, new CreateWorkloadRequest { Name = "  ", Image = "nginx" }));
    }

    [TestMethod]
    public async Task Create_with_blank_image_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo()).CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "" }));
    }

    [TestMethod]
    public async Task Create_with_duplicate_name_throws_conflict()
    {
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync("web", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            Build(repo).CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx" }));
    }

    [TestMethod]
    public async Task Create_with_negative_replicas_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo()).CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx", Replicas = -1 }));
    }

    [TestMethod]
    public async Task Create_with_out_of_range_port_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "web",
                Image = "nginx",
                Ports = [new PortMapping(0, 80, PortProtocol.Tcp)],
            }));
    }

    [TestMethod]
    public async Task Get_missing_throws_not_found()
    {
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("x", It.IsAny<CancellationToken>())).ReturnsAsync((Workload?)null);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => Build(repo).GetAsync(Admin, "x"));
    }

    [TestMethod]
    public async Task Deploy_builds_deployment_on_managed_network_and_calls_backend()
    {
        var repo = Repo();
        var workload = new Workload { Id = "w1", Name = "web", Image = "nginx", Replicas = 2, Ports = [new PortMapping(8080, 80, PortProtocol.Tcp)] };
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var backend = new Mock<IWorkloadBackend>();
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = true, DesiredReplicas = 2, RunningReplicas = 2, State = WorkloadState.Running });

        var status = await Build(repo, backend).DeployAsync(Admin, "w1");

        Assert.AreEqual("fbsm--web", sent!.Name);
        Assert.AreEqual("nginx", sent.Image);
        Assert.AreEqual(2, sent.Replicas);
        Assert.AreEqual("fbsm-overlay", sent.Network);
        Assert.HasCount(1, sent.Ports);
        Assert.AreEqual(WorkloadState.Running, status.State);
    }

    [TestMethod]
    public async Task Scale_persists_replicas_and_calls_backend()
    {
        var repo = Repo();
        var workload = new Workload { Id = "w1", Name = "web", Image = "nginx", Replicas = 1 };
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var backend = new Mock<IWorkloadBackend>();
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = true, DesiredReplicas = 5, RunningReplicas = 5, State = WorkloadState.Running });

        await Build(repo, backend).ScaleAsync(Admin, "w1", 5);

        Assert.AreEqual(5, workload.Replicas);
        repo.Verify(r => r.UpdateAsync(workload, It.IsAny<CancellationToken>()), Times.Once);
        backend.Verify(b => b.ScaleAsync(It.Is<WorkloadDeployment>(d => d.Name == "fbsm--web"), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Scale_negative_throws_and_never_touches_the_backend()
    {
        var backend = new Mock<IWorkloadBackend>();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(Repo(), backend).ScaleAsync(Admin, "w1", -1));

        backend.Verify(b => b.ScaleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Create_native_on_agent_stores_command_and_resolves_agent_name()
    {
        var repo = Repo();
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.FindByIdAsync("ag1", It.IsAny<CancellationToken>())).ReturnsAsync(new Agent { Id = "ag1", Name = "win-1" });
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        var result = await Build(repo, agents: agents).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "reporter",
            Target = WorkloadTarget.Agent,
            AgentId = "ag1",
            Command = "reporter.exe",
            Args = ["--verbose"],
        });

        Assert.AreEqual(WorkloadKind.Native, result.Kind);
        Assert.AreEqual("ag1", saved!.AgentId);
        Assert.AreEqual("reporter.exe", saved.Command);
        Assert.AreEqual("win-1", result.AgentName);
        Assert.IsNull(result.Image);
    }

    [TestMethod]
    public async Task Create_native_with_unknown_agent_throws_validation()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), agents: agents).CreateAsync(Admin, new CreateWorkloadRequest { Name = "x", Target = WorkloadTarget.Agent, AgentId = "nope", Command = "c" }));
    }

    [TestMethod]
    public async Task Create_native_without_command_throws_validation()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.FindByIdAsync("ag1", It.IsAny<CancellationToken>())).ReturnsAsync(new Agent { Id = "ag1", Name = "a" });

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), agents: agents).CreateAsync(Admin, new CreateWorkloadRequest { Name = "x", Target = WorkloadTarget.Agent, AgentId = "ag1", Command = " " }));
    }

    [TestMethod]
    public async Task Create_with_valid_group_sets_group_and_resolves_name()
    {
        var repo = Repo();
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.FindByIdAsync("grp1", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkloadGroup { Id = "grp1", Name = "prod" });
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        var result = await Build(repo, groups: groups).CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx", GroupId = "grp1" });

        Assert.AreEqual("grp1", saved!.GroupId);
        Assert.AreEqual("grp1", result.GroupId);
        Assert.AreEqual("prod", result.GroupName);
    }

    [TestMethod]
    public async Task Create_with_unknown_group_throws_validation()
    {
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((WorkloadGroup?)null);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), groups: groups).CreateAsync(Admin, new CreateWorkloadRequest { Name = "web", Image = "nginx", GroupId = "nope" }));
    }

    private static (Mock<IWorkloadRepository> repo, Mock<IWorkloadBackend> backend) Deployable(Workload workload)
    {
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync(workload.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workload);
        var backend = new Mock<IWorkloadBackend>();
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = workload.Name, Deployed = true, State = WorkloadState.Running });
        return (repo, backend);
    }

    [TestMethod]
    public async Task Deploy_records_a_revision()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx:1.27", Replicas = 2 };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend).DeployAsync(Admin, "w1");

        Assert.HasCount(1, workload.Revisions);
        Assert.AreEqual(1, workload.Revisions[0].Number);
        Assert.AreEqual("nginx:1.27", workload.Revisions[0].Image);
    }

    [TestMethod]
    public async Task A_rolled_back_deploy_does_not_become_the_running_revision()
    {
        // Swarm accepted the update, watched the new task fail its health check, and put v1 back. The
        // platform used to record v2 as running and report no pending changes.
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");

        workload.Image = "v2";
        await svc.DeployAsync(Admin, "w1");
        await svc.SettleRevisionAsync("w1", new WorkloadRuntimeStatus
        {
            Name = "web",
            Deployed = true,
            UpdateState = "rollback_completed",
        });

        var result = await svc.GetAsync(Admin, "w1");
        Assert.AreEqual(1, result.CurrentRevision, "v1 is what the cluster is holding");
        Assert.IsTrue(result.HasPendingChanges, "the v2 edit is still waiting to be deployed");
        Assert.HasCount(1, workload.Revisions);
    }

    [TestMethod]
    public async Task Settling_the_same_rollback_twice_does_not_eat_the_running_revision()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");
        workload.Image = "v2";
        await svc.DeployAsync(Admin, "w1");

        // Swarm keeps reporting the rollback long after it happened, and the reconcile runs every minute.
        var rolledBack = new WorkloadRuntimeStatus { Name = "web", Deployed = true, UpdateState = "rollback_completed" };
        await svc.SettleRevisionAsync("w1", rolledBack);
        await svc.SettleRevisionAsync("w1", rolledBack);
        await svc.SettleRevisionAsync("w1", rolledBack);

        Assert.HasCount(1, workload.Revisions);
        Assert.AreEqual(1, (await svc.GetAsync(Admin, "w1")).CurrentRevision);
    }

    [TestMethod]
    public async Task A_rollout_still_in_flight_leaves_the_previous_revision_running()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");

        workload.Image = "v2";
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = true, UpdateState = "updating" });
        await svc.DeployAsync(Admin, "w1");

        Assert.AreEqual(1, (await svc.GetAsync(Admin, "w1")).CurrentRevision, "v1 serves until v2 converges");
    }

    [TestMethod]
    public async Task A_native_deploy_is_running_as_soon_as_the_agent_takes_it()
    {
        // There is no rollout to wait for and nothing will come back to settle it.
        var workload = new Workload
        {
            Id = "w1", Name = "svc", Target = WorkloadTarget.Agent, Kind = WorkloadKind.Native,
            AgentId = "a1", Command = "run.exe",
        };
        var (repo, backend) = Deployable(workload);

        var svc = Build(repo, backend, agents: WithAgent());
        await svc.DeployAsync(Admin, "w1");

        Assert.AreEqual(1, (await svc.GetAsync(Admin, "w1")).CurrentRevision);

        // And again on a redeploy — the status sweep that settles container rollouts never sees a
        // native workload, so if the deploy doesn't settle it nothing will.
        workload.Command = "run2.exe";
        await svc.DeployAsync(Admin, "w1");

        var after = await svc.GetAsync(Admin, "w1");
        Assert.AreEqual(2, after.CurrentRevision);
        Assert.IsFalse(after.HasPendingChanges);
    }

    [TestMethod]
    public async Task Deploy_twice_without_a_change_keeps_a_single_revision()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");
        await svc.DeployAsync(Admin, "w1");

        Assert.HasCount(1, workload.Revisions);
    }

    [TestMethod]
    public async Task Deploy_after_changes_adds_revisions_and_prunes_to_the_last_three()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        foreach (var image in new[] { "v1", "v2", "v3", "v4" })
        {
            workload.Image = image;
            await svc.DeployAsync(Admin, "w1");
        }

        Assert.HasCount(3, workload.Revisions);
        Assert.AreEqual(2, workload.Revisions.First().Number); // 1 was pruned
        Assert.AreEqual(4, workload.Revisions.Last().Number);
        Assert.AreEqual("v4", workload.Revisions.Last().Image);
    }

    [TestMethod]
    public async Task Revert_loads_an_old_revision_into_the_desired_config()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "new", Replicas = 5,
            Revisions = [new WorkloadRevision { Number = 1, Image = "old", Replicas = 2 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Build(repo).RevertAsync(Admin, "w1", 1);

        Assert.AreEqual("old", workload.Image);
        Assert.AreEqual(2, workload.Replicas);
    }

    [TestMethod]
    public async Task Revert_to_missing_revision_throws_not_found()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "x" };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => Build(repo).RevertAsync(Admin, "w1", 9));
    }

    [TestMethod]
    public async Task Pending_changes_flag_set_when_saved_config_differs_from_running_revision()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v2", Replicas = 1,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var result = await Build(repo).GetAsync(Admin, "w1");

        Assert.IsTrue(result.HasPendingChanges);
        Assert.AreEqual(1, result.CurrentRevision);
    }

    [TestMethod]
    public async Task Restart_before_deploy_throws_conflict()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "x" };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).RestartAsync(Admin, "w1"));
        backend.Verify(b => b.RestartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Restart_of_a_stopped_workload_throws_conflict()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx",
            DesiredState = WorkloadDesiredState.Stopped,
            Revisions = [new WorkloadRevision { Number = 1, Image = "nginx", Replicas = 2 }],
        };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).RestartAsync(Admin, "w1"));

        backend.Verify(b => b.RestartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(WorkloadDesiredState.Stopped, workload.DesiredState, "a refused restart must not claim it is running");
    }

    [TestMethod]
    public async Task Restart_of_an_undeployed_workload_throws_conflict()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx",
            Revisions = [new WorkloadRevision { Number = 1, Image = "nginx", Replicas = 2 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);
        var backend = new Mock<IWorkloadBackend>();
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = false, State = WorkloadState.NotDeployed });

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).RestartAsync(Admin, "w1"));

        backend.Verify(b => b.RestartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_source_needs_the_agent_to_own_the_directory()
    {
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "srcds",
            ManagedDirectory = false, Source = new WorkloadSourceRequest { Kind = SourceKind.Zip, Url = "https://example.com/s.zip" },
        }));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not-a-url")]
    [DataRow("ftp://example.com/s.zip")]
    [DataRow("file:///etc/passwd")]
    public async Task A_source_url_must_be_http(string? url)
    {
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "srcds",
            ManagedDirectory = true, Source = new WorkloadSourceRequest { Kind = SourceKind.Zip, Url = url },
        }));
    }

    [TestMethod]
    public async Task A_steam_source_needs_an_app_id()
    {
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "srcds", ManagedDirectory = true,
            Source = new WorkloadSourceRequest { Kind = SourceKind.SteamCmd },
        }));
    }

    [TestMethod]
    public async Task A_steam_account_without_a_password_is_refused()
    {
        // A username with no password can never log in, and anonymous ignores both — so this is always
        // a mistake rather than a valid anonymous install.
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "srcds", ManagedDirectory = true,
            Source = new WorkloadSourceRequest { Kind = SourceKind.SteamCmd, SteamAppId = 740, SteamUsername = "me" },
        }));
    }

    [TestMethod]
    public async Task The_steam_password_is_encrypted_and_never_returned()
    {
        var repo = Repo();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        var response = await Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "srcds", ManagedDirectory = true,
            Source = new WorkloadSourceRequest { Kind = SourceKind.SteamCmd, SteamAppId = 740, SteamUsername = "me", SteamPassword = "hunter2" },
        });

        StringAssert.Contains(saved!.Source.SteamPasswordEnc!, "hunter2", "the test protector keeps the plaintext visible");
        Assert.AreNotEqual("hunter2", saved.Source.SteamPasswordEnc, "it must not be stored as-is");
        Assert.IsTrue(response.Source!.HasSteamPassword);
    }

    [TestMethod]
    public async Task A_blank_steam_password_keeps_the_stored_one()
    {
        // Otherwise editing the branch would either wipe the password or force the client to round-trip
        // the secret — and re-encrypting would move the config signature and fake a pending change.
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "srcds", ManagedDirectory = true,
            Source = new WorkloadSource { Kind = SourceKind.SteamCmd, SteamAppId = 740, SteamUsername = "me", SteamPasswordEnc = "enc9:hunter2" },
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Build(repo, agents: WithAgent()).UpdateAsync(Admin, "w1", new UpdateWorkloadRequest
        {
            Command = "srcds", ManagedDirectory = true,
            Source = new WorkloadSourceRequest { Kind = SourceKind.SteamCmd, SteamAppId = 740, SteamUsername = "me", SteamBranch = "beta" },
        });

        Assert.AreEqual("enc9:hunter2", workload.Source.SteamPasswordEnc);
        Assert.AreEqual("beta", workload.Source.SteamBranch);
    }

    [TestMethod]
    public async Task Update_without_a_source_throws_conflict()
    {
        var workload = new Workload { Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "srcds" };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).UpdateAsync(Admin, "w1"));
        backend.Verify(b => b.UpdateAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_fetches_the_saved_source_not_the_running_revision()
    {
        // An update is about files, so a corrected URL must take effect without a deploy first.
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "srcds",
            ManagedDirectory = true,
            Source = new WorkloadSource { Kind = SourceKind.Zip, Url = "https://example.com/new.zip" },
            Revisions = [new WorkloadRevision { Number = 1, Command = "srcds", Source = new WorkloadSource { Kind = SourceKind.Zip, Url = "https://example.com/old.zip" } }],
        };
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? sent = null;
        backend.Setup(b => b.UpdateAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => sent = d).Returns(Task.CompletedTask);

        await Build(repo, backend).UpdateAsync(Admin, "w1");

        Assert.AreEqual("https://example.com/new.zip", sent?.Source?.Url);
    }

    [TestMethod]
    public async Task Console_before_deploy_throws_conflict()
    {
        var workload = new Workload { Id = "w1", Name = "srv", Kind = WorkloadKind.Native, Command = "srv.exe", AgentId = "a1" };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).SendConsoleAsync(Admin, "w1", "save"));
        backend.Verify(b => b.SendConsoleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Console_rejects_an_empty_command(string text)
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srv", Kind = WorkloadKind.Native, Command = "srv.exe", AgentId = "a1",
            Revisions = [new WorkloadRevision { Number = 1, Command = "srv.exe" }],
        };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, backend).SendConsoleAsync(Admin, "w1", text));
        backend.Verify(b => b.SendConsoleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Console_sends_the_trimmed_command_against_the_running_revision()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srv", Kind = WorkloadKind.Native, Command = "pending.exe", AgentId = "a1",
            Revisions = [new WorkloadRevision { Number = 1, Command = "running.exe" }],
        };
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? sent = null;
        string? text = null;
        backend.Setup(b => b.SendConsoleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, string, CancellationToken>((d, t, _) => { sent = d; text = t; })
            .Returns(Task.CompletedTask);

        await Build(repo, backend).SendConsoleAsync(Admin, "w1", "  save  ");

        Assert.AreEqual("save", text);
        Assert.AreEqual("running.exe", sent?.Command, "console goes to what is running, not to unsaved edits");
    }

    [TestMethod]
    public async Task Start_before_deploy_throws_conflict()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "x" };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).StartAsync(Admin, "w1"));
        backend.Verify(b => b.StartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Start_undoes_a_stop_without_publishing_pending_edits()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "pending-edit", Replicas = 9,
            DesiredState = WorkloadDesiredState.Stopped,
            Revisions = [new WorkloadRevision { Number = 1, Image = "running", Replicas = 2 }],
        };
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? started = null;
        backend.Setup(b => b.StartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => started = d).Returns(Task.CompletedTask);

        await Build(repo, backend).StartAsync(Admin, "w1");

        Assert.AreEqual("running", started!.Image);
        Assert.AreEqual(2, started.Replicas, "the replica count it had before the stop");
        Assert.AreEqual(WorkloadDesiredState.Running, workload.DesiredState);
    }

    [TestMethod]
    public async Task Restart_bounces_the_running_revision_config_not_pending_edits()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "pending-edit", Replicas = 9,
            Revisions = [new WorkloadRevision { Number = 1, Image = "running", Replicas = 2 }],
        };
        var (repo, backend) = Deployable(workload);
        WorkloadDeployment? restarted = null;
        backend.Setup(b => b.RestartAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => restarted = d).Returns(Task.CompletedTask);

        await Build(repo, backend).RestartAsync(Admin, "w1");

        Assert.AreEqual("running", restarted!.Image); // the deployed revision, not the unsaved edit
        Assert.AreEqual(2, restarted.Replicas);
    }

    [TestMethod]
    public async Task Pending_changes_flag_set_when_only_the_memory_limit_differs()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1, MemoryLimitMb = 1024,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1, MemoryLimitMb = 512 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var result = await Build(repo).GetAsync(Admin, "w1");

        Assert.IsTrue(result.HasPendingChanges);
    }

    [TestMethod]
    public async Task No_pending_changes_when_saved_matches_running_revision()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 3,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 3 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var result = await Build(repo).GetAsync(Admin, "w1");

        Assert.IsFalse(result.HasPendingChanges);
    }

    private static Mock<IAgentRepository> WithAgent(string id = "a1")
    {
        var agents = new Mock<IAgentRepository>();
        var agent = new Agent { Id = id, Name = "box" };
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent> { agent });
        agents.Setup(a => a.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        return agents;
    }

    [TestMethod]
    public async Task Create_native_defaults_to_restart_on_failure_with_ten_second_grace()
    {
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
        });

        Assert.AreEqual(RestartPolicy.OnFailure, saved!.RestartPolicy);
        Assert.AreEqual(10, saved.StopGraceSeconds);
    }

    [TestMethod]
    public async Task Create_native_with_out_of_range_stop_grace_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
                StopGraceSeconds = 301,
            }));
    }

    [TestMethod]
    public async Task Deploy_snapshots_restart_policy_and_stop_grace()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "/srv/srcds",
            RestartPolicy = RestartPolicy.Always, StopGraceSeconds = 45, StopCommand = "quit", ManagedDirectory = true,
        };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend, WithAgent()).DeployAsync(Admin, "w1");

        Assert.AreEqual(RestartPolicy.Always, workload.Revisions[0].RestartPolicy);
        Assert.AreEqual(45, workload.Revisions[0].StopGraceSeconds);
        Assert.AreEqual("quit", workload.Revisions[0].StopCommand);
        Assert.IsTrue(workload.Revisions[0].ManagedDirectory);
    }

    [TestMethod]
    public async Task Pending_changes_flag_set_when_only_the_managed_directory_toggle_differs()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, Command = "srcds", ManagedDirectory = true,
            Revisions = [new WorkloadRevision { Number = 1, Command = "srcds", ManagedDirectory = false }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        Assert.IsTrue((await Build(repo).GetAsync(Admin, "w1")).HasPendingChanges);
    }

    [TestMethod]
    public async Task Pending_changes_flag_set_when_only_the_stop_command_differs()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, Command = "/srv/srcds", StopCommand = "quit",
            Revisions = [new WorkloadRevision { Number = 1, Command = "/srv/srcds", StopCommand = "stop" }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        Assert.IsTrue((await Build(repo).GetAsync(Admin, "w1")).HasPendingChanges);
    }

    [TestMethod]
    public async Task Pending_changes_flag_set_when_only_the_restart_policy_differs()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, Command = "/srv/srcds",
            RestartPolicy = RestartPolicy.Always, StopGraceSeconds = 10,
            Revisions = [new WorkloadRevision { Number = 1, Command = "/srv/srcds", RestartPolicy = RestartPolicy.Never, StopGraceSeconds = 10 }],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        Assert.IsTrue((await Build(repo).GetAsync(Admin, "w1")).HasPendingChanges);
    }

    [TestMethod]
    public async Task Revert_restores_every_snapshotted_field()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "new", Replicas = 5,
            MemoryLimitMb = 2048, CpuLimit = 2.0, Mounts = [new VolumeMount(VolumeMountType.Volume, "new", "/new", false)],
            RestartPolicy = RestartPolicy.Never, StopGraceSeconds = 90, StopCommand = "new-stop",
            Revisions =
            [
                new WorkloadRevision
                {
                    Number = 1, Image = "old", Replicas = 2, MemoryLimitMb = 512, CpuLimit = 0.5,
                    Mounts = [new VolumeMount(VolumeMountType.Volume, "old", "/old", false)],
                    RestartPolicy = RestartPolicy.OnFailure, StopGraceSeconds = 15, StopCommand = "old-stop",
                },
            ],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Build(repo).RevertAsync(Admin, "w1", 1);

        Assert.AreEqual(512, workload.MemoryLimitMb);
        Assert.AreEqual(0.5, workload.CpuLimit);
        Assert.AreEqual("/old", workload.Mounts.Single().Target);
        Assert.AreEqual(RestartPolicy.OnFailure, workload.RestartPolicy);
        Assert.AreEqual(15, workload.StopGraceSeconds);
        Assert.AreEqual("old-stop", workload.StopCommand);
    }

    [TestMethod]
    public async Task Scale_keeps_the_rest_of_the_running_revision_intact()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            // Bind, not a named volume: that one pins to a node and refuses to scale, which isn't the
            // subject here.
            MemoryLimitMb = 512, CpuLimit = 0.5, Mounts = [new VolumeMount(VolumeMountType.Bind, "/srv/data", "/data", false)],
            Revisions =
            [
                new WorkloadRevision
                {
                    Number = 1, Image = "v1", Replicas = 1, MemoryLimitMb = 512, CpuLimit = 0.5,
                    Mounts = [new VolumeMount(VolumeMountType.Bind, "/srv/data", "/data", false)],
                },
            ],
        };
        var (repo, backend) = Deployable(workload);

        var result = await Build(repo, backend).ScaleAsync(Admin, "w1", 4);

        var running = workload.Revisions.Single();
        Assert.AreEqual(4, running.Replicas);
        Assert.AreEqual(512, running.MemoryLimitMb);
        Assert.AreEqual(0.5, running.CpuLimit);
        Assert.AreEqual("/data", running.Mounts.Single().Target);
        Assert.IsFalse((await Build(repo, backend).GetAsync(Admin, "w1")).HasPendingChanges, "scaling must not register as drift");
    }

    [TestMethod]
    public async Task Scaling_a_workload_with_a_health_check_is_not_drift()
    {
        // The revision used to be rebuilt field by field on scale, and the rebuild left the health
        // settings behind — which the config signature reads, so the workload went permanently
        // "pending changes" the first time anyone scaled it.
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            HealthCommand = "curl -f localhost/health", HealthRetries = 5,
            Revisions =
            [
                new WorkloadRevision
                {
                    Number = 1, Image = "v1", Replicas = 1,
                    HealthCommand = "curl -f localhost/health", HealthRetries = 5,
                },
            ],
        };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend).ScaleAsync(Admin, "w1", 3);

        Assert.AreEqual("curl -f localhost/health", workload.Revisions.Single().HealthCommand);
        Assert.IsFalse((await Build(repo, backend).GetAsync(Admin, "w1")).HasPendingChanges);
    }

    [TestMethod]
    public async Task A_reservation_cannot_exceed_its_limit()
    {
        // Reserving space the container is then forbidden to use schedules a node full of nothing.
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "web", Image = "nginx", MemoryLimitMb = 256, MemoryReserveMb = 512,
            }));

        Assert.ContainsSingle(ex.Errors.Keys, nameof(CreateWorkloadRequest.MemoryReserveMb));
    }

    [TestMethod]
    public async Task Changing_a_reservation_is_a_pending_change()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");
        Assert.IsFalse((await svc.GetAsync(Admin, "w1")).HasPendingChanges);

        workload.MemoryReserveMb = 256;
        Assert.IsTrue((await svc.GetAsync(Admin, "w1")).HasPendingChanges);
    }

    [TestMethod]
    public async Task Stop_holds_the_service_rather_than_removing_it()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 2 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");
        await svc.StopAsync(Admin, "w1");

        backend.Verify(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Once);
        backend.Verify(b => b.UndeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(WorkloadDesiredState.Stopped, workload.DesiredState);
    }

    [TestMethod]
    public async Task Deploying_again_undoes_a_stop()
    {
        // Redeploying unchanged config records no revision, so the desired state has to be saved
        // regardless of whether one was written.
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            DesiredState = WorkloadDesiredState.Stopped,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1 }],
        };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend).DeployAsync(Admin, "w1");

        Assert.AreEqual(WorkloadDesiredState.Running, workload.DesiredState);
    }

    [TestMethod]
    public async Task Undeploy_removes_the_service_but_keeps_the_history()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1 }],
        };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend).UndeployAsync(Admin, "w1");

        backend.Verify(b => b.UndeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.HasCount(1, workload.Revisions);
        Assert.AreEqual(WorkloadDesiredState.Stopped, workload.DesiredState);
    }

    [TestMethod]
    public async Task Deleting_a_workload_takes_its_service_with_it()
    {
        // Without this the record goes and the service keeps running, findable only from the docker CLI.
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend).DeleteAsync(Admin, "w1");

        backend.Verify(b => b.UndeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.RemoveAsync(workload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Deleting_a_workload_takes_its_routes_with_it()
    {
        // They drop out of the generated config on their own, but they keep their (hostname, path) —
        // enough to leave a workload recreated under the same name silently without an address.
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var routes = RouteService();
        routes.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new RouteResponse { Id = "r1", Hostname = "web.example.com", Path = "/", WorkloadId = "w1" },
            new RouteResponse { Id = "r2", Hostname = "other.example.com", Path = "/", WorkloadId = "w2" },
        ]);

        await Build(repo, backend, routes: routes).DeleteAsync(Admin, "w1");

        routes.Verify(r => r.DeleteAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        routes.Verify(r => r.DeleteAsync("r2", It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IDeployedSpecSource> LiveSpec(DeployedSpec? spec)
    {
        var source = new Mock<IDeployedSpecSource>();
        source.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(spec);
        return source;
    }

    [TestMethod]
    public async Task A_hand_changed_service_reports_drift()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1 }],
        };
        var (repo, backend) = Deployable(workload);
        var specs = LiveSpec(new DeployedSpec { Image = "v1", Replicas = 4 });

        var drift = await Build(repo, backend, deployedSpecs: specs).GetDriftAsync(Admin, "w1");

        Assert.IsTrue(drift.HasDrift);
        Assert.ContainsSingle(drift.Fields, "replicas");
    }

    [TestMethod]
    public async Task A_deploy_that_has_not_settled_yet_is_not_drifting()
    {
        // Mid-rollout the service already holds the new spec while the running revision is still the
        // old one. Comparing them reports our own redeploy as a change made outside ServerManager.
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            Revisions =
            [
                new WorkloadRevision
                {
                    Number = 1, Image = "v1", Replicas = 1,
                    Ports = [new PortMapping(8080, 80, PortProtocol.Tcp)],
                },
                new WorkloadRevision { Number = 2, Image = "v1", Replicas = 1, Applied = false },
            ],
        };
        var (repo, backend) = Deployable(workload);
        var specs = LiveSpec(new DeployedSpec { Image = "v1", Replicas = 1 });

        Assert.IsFalse((await Build(repo, backend, deployedSpecs: specs).GetDriftAsync(Admin, "w1")).HasDrift);
    }

    [TestMethod]
    public async Task A_stopped_workload_is_not_drifting()
    {
        // Stop holds the service at zero replicas on purpose. Reporting that as drift would put a
        // permanent banner on every stopped workload and teach people to ignore it.
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 3,
            DesiredState = WorkloadDesiredState.Stopped,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 3 }],
        };
        var (repo, backend) = Deployable(workload);
        var specs = LiveSpec(new DeployedSpec { Image = "v1", Replicas = 0 });

        Assert.IsFalse((await Build(repo, backend, deployedSpecs: specs).GetDriftAsync(Admin, "w1")).HasDrift);
    }

    [TestMethod]
    public async Task An_undeployed_workload_is_not_drifting()
    {
        // No service to read back is "not deployed", which the status already says.
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1,
            Revisions = [new WorkloadRevision { Number = 1, Image = "v1", Replicas = 1 }],
        };
        var (repo, backend) = Deployable(workload);

        Assert.IsFalse((await Build(repo, backend, deployedSpecs: LiveSpec(null)).GetDriftAsync(Admin, "w1")).HasDrift);
    }

    [TestMethod]
    public async Task A_native_workload_has_no_spec_to_read_back()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "svc", Target = WorkloadTarget.Agent, Kind = WorkloadKind.Native,
            AgentId = "a1", Command = "run.exe",
            Revisions = [new WorkloadRevision { Number = 1, Command = "run.exe" }],
        };
        var (repo, backend) = Deployable(workload);

        Assert.IsFalse((await Build(repo, backend, agents: WithAgent()).GetDriftAsync(Admin, "w1")).HasDrift);
    }

    [TestMethod]
    public async Task A_global_workload_cannot_be_scaled()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "exporter", Kind = WorkloadKind.Container, Image = "v1",
            Mode = WorkloadMode.Global,
        };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo, backend).ScaleAsync(Admin, "w1", 3));
    }

    [TestMethod]
    public async Task A_global_workload_cannot_also_be_pinned_to_one_node()
    {
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), nodes: WithNode()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "web", Image = "nginx", Mode = WorkloadMode.Global,
                Placement = WorkloadPlacement.Node, NodeId = "node-1",
            }));

        Assert.ContainsSingle(ex.Errors.Keys, nameof(CreateWorkloadRequest.Placement));
    }

    [TestMethod]
    public async Task Switching_to_global_is_a_pending_change()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "v1", Replicas = 1 };
        var (repo, backend) = Deployable(workload);
        var svc = Build(repo, backend);

        await svc.DeployAsync(Admin, "w1");
        workload.Mode = WorkloadMode.Global;

        Assert.IsTrue((await svc.GetAsync(Admin, "w1")).HasPendingChanges);
    }

    private static Mock<IWorkloadRepository> RepoWith(params Workload[] existing)
    {
        var repo = Repo();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        return repo;
    }

    [TestMethod]
    public async Task Native_ports_are_normalized_to_host_bound_with_matching_target()
    {
        var repo = RepoWith();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo, agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
            Ports = [new PortMapping(27015, 0, PortProtocol.Both)],
        });

        var port = saved!.Ports.Single();
        Assert.AreEqual(27015, port.Published);
        Assert.AreEqual(27015, port.Target, "a native process binds one port — there is no container to map into");
        Assert.AreEqual(PortPublishMode.Host, port.Mode);
    }

    [TestMethod]
    public async Task Native_port_clashing_on_the_same_agent_throws_validation()
    {
        var taken = new Workload
        {
            Id = "existing", Name = "cs2", Kind = WorkloadKind.Native, AgentId = "a1",
            Ports = [new PortMapping(27015, 27015, PortProtocol.Both, PortPublishMode.Host)],
        };

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(RepoWith(taken), agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
                Ports = [new PortMapping(27015, 0, PortProtocol.Both)],
            }));
    }

    [TestMethod]
    public async Task The_same_native_port_on_a_different_agent_is_fine()
    {
        var elsewhere = new Workload
        {
            Id = "existing", Name = "cs2", Kind = WorkloadKind.Native, AgentId = "a2",
            Ports = [new PortMapping(27015, 27015, PortProtocol.Both, PortPublishMode.Host)],
        };

        var result = await Build(RepoWith(elsewhere), agents: WithAgent()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "srcds", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "/srv/srcds",
            Ports = [new PortMapping(27015, 0, PortProtocol.Both)],
        });

        Assert.AreEqual(27015, result.Ports.Single().Published);
    }

    [TestMethod]
    public async Task A_native_workload_does_not_clash_with_its_own_ports_on_update()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1", Command = "/srv/srcds",
            Ports = [new PortMapping(27015, 27015, PortProtocol.Both, PortPublishMode.Host)],
        };
        var repo = RepoWith(workload);
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var result = await Build(repo, agents: WithAgent()).UpdateAsync(Admin, "w1", new UpdateWorkloadRequest
        {
            Command = "/srv/srcds", Ports = [new PortMapping(27015, 0, PortProtocol.Both)],
        });

        Assert.AreEqual(27015, result.Ports.Single().Published);
    }

    private static Mock<IAgentRepository> WithAgents(params string[] ids)
    {
        var agents = new Mock<IAgentRepository>();
        var known = ids.Select(id => new Agent { Id = id, Name = id }).ToList();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(known);
        foreach (var agent in known)
        {
            agents.Setup(a => a.FindByIdAsync(agent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        }

        return agents;
    }

    private static Workload NativeOn(string agentId, bool deployed, params PortMapping[] ports)
    {
        var workload = new Workload
        {
            Id = "w1", Name = "srcds", Target = WorkloadTarget.Agent, Kind = WorkloadKind.Native,
            AgentId = agentId, Command = "/srv/srcds", Ports = [.. ports],
        };

        if (deployed)
        {
            // Replicas mirrors the workload's default — a real revision is snapshotted from it.
            workload.Revisions = [new WorkloadRevision { Number = 1, Replicas = 1, Command = "/srv/srcds", Ports = [.. ports] }];
        }

        return workload;
    }

    private static (Mock<IWorkloadRepository> repo, Mock<IWorkloadBackend> backend) Movable(Workload workload, params Workload[] others)
    {
        var repo = RepoWith([workload, .. others]);
        repo.Setup(r => r.FindByIdAsync(workload.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workload);
        return (repo, new Mock<IWorkloadBackend>());
    }

    [TestMethod]
    public async Task Move_stops_on_the_old_agent_then_deploys_on_the_new()
    {
        var workload = NativeOn("a1", deployed: true);
        var (repo, backend) = Movable(workload);
        var dispatched = new List<(string Op, string? AgentId)>();
        backend.Setup(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => dispatched.Add(("stop", d.AgentId)))
            .Returns(Task.CompletedTask);
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadDeployment, CancellationToken>((d, _) => dispatched.Add(("deploy", d.AgentId)))
            .Returns(Task.CompletedTask);

        var result = await Build(repo, backend, agents: WithAgents("a1", "a2")).MoveAsync(Admin, "w1", "a2");

        CollectionAssert.AreEqual(new[] { ("stop", "a1"), ("deploy", "a2") }, dispatched, "stop where it runs before starting it elsewhere");
        Assert.AreEqual("a2", workload.AgentId);
        Assert.AreEqual("a2", result.Workload.AgentId);
        Assert.IsFalse(result.Workload.HasPendingChanges, "a move carries the running revision across — nothing to redeploy");
        repo.Verify(r => r.UpdateAsync(workload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Move_of_a_workload_that_was_never_deployed_only_reassigns_it()
    {
        var workload = NativeOn("a1", deployed: false);
        var (repo, backend) = Movable(workload);

        var result = await Build(repo, backend, agents: WithAgents("a1", "a2")).MoveAsync(Admin, "w1", "a2");

        Assert.AreEqual("a2", workload.AgentId);
        Assert.IsFalse(result.TargetAgentOffline, "nothing was waiting to start");
        backend.Verify(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
        backend.Verify(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Move_from_an_offline_agent_skips_the_stop_and_reports_it()
    {
        var workload = NativeOn("a1", deployed: true);
        var (repo, backend) = Movable(workload);

        var result = await Build(repo, backend, agents: WithAgents("a1", "a2"), connections: Online("a2")).MoveAsync(Admin, "w1", "a2");

        Assert.IsTrue(result.PreviousAgentOffline);
        Assert.AreEqual("a2", workload.AgentId);
        backend.Verify(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
        backend.Verify(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Move_to_an_offline_agent_skips_the_deploy_and_reports_it()
    {
        var workload = NativeOn("a1", deployed: true);
        var (repo, backend) = Movable(workload);

        var result = await Build(repo, backend, agents: WithAgents("a1", "a2"), connections: Online("a1")).MoveAsync(Admin, "w1", "a2");

        Assert.IsTrue(result.TargetAgentOffline);
        Assert.AreEqual("a2", workload.AgentId, "reconcile starts it when that agent connects");
        backend.Verify(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Once);
        backend.Verify(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Move_to_the_agent_it_already_runs_on_throws_validation()
    {
        var workload = NativeOn("a1", deployed: true);
        var (repo, backend) = Movable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, backend, agents: WithAgents("a1")).MoveAsync(Admin, "w1", "a1"));
    }

    [TestMethod]
    public async Task Move_to_an_unknown_agent_throws_validation()
    {
        var workload = NativeOn("a1", deployed: true);
        var (repo, backend) = Movable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, backend, agents: WithAgents("a1")).MoveAsync(Admin, "w1", "nope"));
    }

    [TestMethod]
    public async Task Move_of_a_container_workload_throws_validation()
    {
        var workload = new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx" };
        var (repo, backend) = Movable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, backend, agents: WithAgents("a1")).MoveAsync(Admin, "w1", "a1"));
    }

    [TestMethod]
    public async Task Move_onto_an_agent_already_using_the_port_throws_and_leaves_it_put()
    {
        var port = new PortMapping(27015, 27015, PortProtocol.Both, PortPublishMode.Host);
        var workload = NativeOn("a1", deployed: true, port);
        var incumbent = new Workload { Id = "other", Name = "cs2", Kind = WorkloadKind.Native, AgentId = "a2", Ports = [port] };
        var (repo, backend) = Movable(workload, incumbent);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(repo, backend, agents: WithAgents("a1", "a2")).MoveAsync(Admin, "w1", "a2"));

        Assert.AreEqual("a1", workload.AgentId, "a rejected move must not half-apply");
        backend.Verify(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<INodeService> WithNode(string id = "n1")
    {
        var nodes = new Mock<INodeService>();
        nodes.Setup(n => n.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NodeResponse> { new() { Id = id, Hostname = "box-1" } });
        return nodes;
    }

    [TestMethod]
    public async Task Published_ports_are_always_host_bound()
    {
        // The ingress mesh SNATs, so the container would see the gateway instead of the real client.
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "cs2", Image = "cs2:latest",
            Ports = [new PortMapping(27015, 27015, PortProtocol.Both)],
        });

        Assert.AreEqual(PortPublishMode.Host, saved!.Ports.Single().Mode);
    }

    [TestMethod]
    public async Task Publishing_ports_neither_pins_a_node_nor_caps_the_replicas()
    {
        // Swarm treats a host port as a node resource, so it spreads the tasks across nodes itself.
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "edge", Image = "haproxy", Replicas = 3,
            Ports = [new PortMapping(8080, 80, PortProtocol.Tcp)],
        });

        Assert.AreEqual(3, saved!.Replicas);
        Assert.IsNull(saved.NodeId);
    }

    [TestMethod]
    public async Task Choosing_the_node_records_it_and_a_missing_one_throws()
    {
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo, nodes: WithNode()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "gpu", Image = "cuda:1", Placement = WorkloadPlacement.Node, NodeId = "n1",
        });

        Assert.AreEqual("n1", saved!.NodeId);

        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            Build(Repo(), nodes: WithNode()).CreateAsync(Admin, new CreateWorkloadRequest
            {
                Name = "gpu2", Image = "cuda:1", Placement = WorkloadPlacement.Node,
            }));
    }

    [TestMethod]
    public async Task A_volume_holds_a_workload_to_a_single_instance()
    {
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "db", Image = "postgres:17", Replicas = 3,
            Mounts = [new VolumeMount(VolumeMountType.Volume, "db-data", "/var/lib/postgresql/data", false)],
        });

        Assert.AreEqual(1, saved!.Replicas, "three copies of a one-node volume is three different disks");
    }

    [TestMethod]
    public async Task Choosing_a_node_holds_it_to_a_single_instance()
    {
        var repo = Repo();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await Build(repo, nodes: WithNode()).CreateAsync(Admin, new CreateWorkloadRequest
        {
            Name = "gpu", Image = "cuda:1", Placement = WorkloadPlacement.Node, NodeId = "n1", Replicas = 3,
        });

        Assert.AreEqual(1, saved!.Replicas, "a chosen node is a choice of one");
    }

    [TestMethod]
    public async Task Scaling_a_workload_pinned_to_a_node_throws()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "gpu", Kind = WorkloadKind.Container, Image = "cuda:1", Replicas = 1,
            Placement = WorkloadPlacement.Node, NodeId = "n1",
        };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, backend, nodes: WithNode()).ScaleAsync(Admin, "w1", 3));
        backend.Verify(b => b.ScaleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Scaling_a_workload_with_a_volume_throws()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "db", Kind = WorkloadKind.Container, Image = "postgres:17", Replicas = 1,
            Mounts = [new VolumeMount(VolumeMountType.Volume, "db-data", "/data", false)],
        };
        var (repo, backend) = Deployable(workload);

        await Assert.ThrowsExactlyAsync<ValidationException>(() => Build(repo, backend, nodes: WithNode()).ScaleAsync(Admin, "w1", 3));
        backend.Verify(b => b.ScaleAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_workload_without_a_volume_scales_past_the_node_count()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx", Replicas = 1,
        };
        var (repo, backend) = Deployable(workload);

        await Build(repo, backend, nodes: WithNode()).ScaleAsync(Admin, "w1", 5);

        Assert.AreEqual(5, workload.Replicas);
    }

    [TestMethod]
    public async Task A_volume_workload_remembers_the_node_it_first_ran_on()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "db", Kind = WorkloadKind.Container, Image = "postgres:17", Replicas = 1,
            Mounts = [new VolumeMount(VolumeMountType.Volume, "db-data", "/data", false)],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Build(repo).SettleRevisionAsync("w1", RunningOn("n7"));

        Assert.AreEqual("n7", workload.PlacedNodeId);
        repo.Verify(r => r.UpdateAsync(workload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task A_remembered_node_is_never_revised()
    {
        // Re-learning would quietly bless whatever empty volume it landed on next as the real one.
        var workload = new Workload
        {
            Id = "w1", Name = "db", Kind = WorkloadKind.Container, Image = "postgres:17", Replicas = 1,
            PlacedNodeId = "n7",
            Mounts = [new VolumeMount(VolumeMountType.Volume, "db-data", "/data", false)],
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Build(repo).SettleRevisionAsync("w1", RunningOn("n9"));

        Assert.AreEqual("n7", workload.PlacedNodeId);
    }

    [TestMethod]
    public async Task A_workload_without_a_volume_remembers_nothing()
    {
        var workload = new Workload
        {
            Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx", Replicas = 2,
        };
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        await Build(repo).SettleRevisionAsync("w1", RunningOn("n7"));

        Assert.IsNull(workload.PlacedNodeId, "nothing ties it down, so the scheduler stays free to move it");
    }

    private static WorkloadRuntimeStatus RunningOn(string nodeId) => new()
    {
        Name = "fbsm--db",
        Deployed = true,
        State = WorkloadState.Running,
        RunningReplicas = 1,
        DesiredReplicas = 1,
        Tasks =
        [
            new WorkloadTask
            {
                Id = "t1", Slot = 1, NodeId = nodeId,
                State = WorkloadTaskState.Running, DesiredState = WorkloadTaskState.Running,
            },
        ],
    };

    [TestMethod]
    public async Task MoveTargets_lists_every_agent_except_the_one_it_is_on()
    {
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(new Workload
        {
            Id = "w1", Name = "reporter", Kind = WorkloadKind.Native, Target = WorkloadTarget.Agent,
            AgentId = "ag1", Command = "reporter.exe",
        });

        var agents = new Mock<IAgentRepository>();
        var svc = Build(repo, agents: agents, connections: Online("ag2"));

        // Build seeds every agent mock with an empty ListAsync, so the real one has to be set after it.
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Agent { Id = "ag1", Name = "win-1" },
            new Agent { Id = "ag2", Name = "win-2" },
            new Agent { Id = "ag3", Name = "win-3" },
        ]);

        var targets = await svc.MoveTargetsAsync(Admin, "w1");

        CollectionAssert.AreEqual(new[] { "ag2", "ag3" }, targets.Select(t => t.Id).ToArray());
        Assert.AreEqual(AgentStatus.Online, targets.Single(t => t.Id == "ag2").Status);
        Assert.AreEqual(AgentStatus.Offline, targets.Single(t => t.Id == "ag3").Status);
    }

    [TestMethod]
    public async Task MoveTargets_needs_Configure_so_the_fleet_is_not_visible_to_a_mere_operator()
    {
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(new Workload
        {
            Id = "w1", Name = "reporter", Kind = WorkloadKind.Native, Target = WorkloadTarget.Agent,
            AgentId = "ag1", Command = "reporter.exe",
        });

        var operate = Build(repo, grants:
            [new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.Operate }]);
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => operate.MoveTargetsAsync(new Caller("u1", false), "w1"));

        // A workload they can't see at all stays unprobeable — 404, not 403.
        var stranger = Build(repo, grants: []);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => stranger.MoveTargetsAsync(new Caller("u2", false), "w1"));
    }

    [TestMethod]
    public async Task MoveTargets_rejects_a_container_workload()
    {
        var repo = Repo();
        repo.Setup(r => r.FindByIdAsync("w1", It.IsAny<CancellationToken>())).ReturnsAsync(new Workload
        {
            Id = "w1", Name = "site", Kind = WorkloadKind.Container, Target = WorkloadTarget.Swarm, Image = "nginx",
        });

        await Assert.ThrowsExactlyAsync<ConflictException>(() => Build(repo).MoveTargetsAsync(Admin, "w1"));
    }
}
