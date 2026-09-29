using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

/// uses the real AccessMap so callers resolve the same way a request does
[TestClass]
public class WorkloadAccessTests
{
    private static readonly Caller Admin = new("admin", IsAdmin: true);
    private static readonly Caller Friend = new("u1", IsAdmin: false);

    private static AccessGrant Grant(AccessLevel level, AccessScope scope = AccessScope.Workload, string target = "w1")
        => new() { SubjectId = "u1", Scope = scope, TargetId = target, Level = level };

    private static Workload Container(string? groupId = null) => new()
    {
        Id = "w1",
        Name = "web",
        GroupId = groupId,
        Target = WorkloadTarget.Swarm,
        Kind = WorkloadKind.Container,
        Image = "nginx:1.27",
        Replicas = 1,
    };

    private sealed class ReversibleProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc:{plaintext}";

        public string Unprotect(string ciphertext) => ciphertext[(ciphertext.IndexOf(':') + 1)..];
    }

    private static (WorkloadService Svc, Mock<IAccessGrantRepository> Grants, Mock<IWorkloadBackend> Backend) Build(
        Workload workload,
        IReadOnlyList<AccessGrant>? grants = null,
        IReadOnlyList<WorkloadGroup>? groups = null,
        IReadOnlyList<Workload>? others = null,
        IReadOnlyList<RouteResponse>? routes = null,
        params Team[] teams)
    {
        var all = new List<Workload> { workload };
        all.AddRange(others ?? []);

        var repo = new Mock<IWorkloadRepository>();
        foreach (var w in all)
        {
            repo.Setup(r => r.FindByIdAsync(w.Id, It.IsAny<CancellationToken>())).ReturnsAsync(w);
        }

        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(all);
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.RemoveAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var grantRepo = new Mock<IAccessGrantRepository>();
        grantRepo.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GrantSubject> subjects, CancellationToken _) =>
                grants?.Where(g => subjects.Contains(new GrantSubject(g.SubjectType, g.SubjectId))).ToList() ?? []);
        grantRepo.Setup(g => g.RemoveForTargetAsync(It.IsAny<AccessScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var groupRepo = new Mock<IWorkloadGroupRepository>();
        groupRepo.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups?.ToList() ?? []);
        groupRepo.Setup(g => g.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => groups?.FirstOrDefault(g => g.Id == id));

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent>());

        var backend = new Mock<IWorkloadBackend>();
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = true });
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns(backend.Object);

        var nodes = new Mock<INodeService>();
        nodes.Setup(n => n.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NodeResponse>());
        var settings = new Mock<IPlatformSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformSettings());

        var routeService = new Mock<IRouteService>();
        routeService.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(routes?.ToList() ?? []);

        var protector = new ReversibleProtector();
        var cluster = Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" });
        var access = new WorkloadAccess(grantRepo.Object, groupRepo.Object, TeamRepo.Of(teams));
        var loader = new WorkloadLoader(repo.Object, access);
        var factory = new WorkloadDeploymentFactory(protector, cluster);
        var lifecycle = new WorkloadLifecycleService(
            loader, repo.Object, access, resolver.Object, new Mock<IDeployedSpecSource>().Object,
            new ClusterState(), factory, TimeProvider.System);

        var svc = new WorkloadService(
            repo.Object,
            loader,
            lifecycle,
            agents.Object,
            new Mock<IAgentRegistry>().Object,
            groupRepo.Object,
            access,
            grantRepo.Object,
            resolver.Object,
            routeService.Object,
            settings.Object,
            nodes.Object,
            protector,
            factory,
            TimeProvider.System);

        return (svc, grantRepo, backend);
    }

    [TestMethod]
    public async Task An_admin_never_touches_the_grant_store()
    {
        var (svc, grants, _) = Build(Container());

        await svc.GetAsync(Admin, "w1");

        grants.Verify(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task A_workload_you_hold_no_grant_on_looks_like_it_is_not_there()
    {
        var (svc, _, _) = Build(Container());

        // 404 not 403, a 403 confirms the id exists
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.GetAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task View_can_read_but_not_restart()
    {
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.View)]);

        Assert.AreEqual("web", (await svc.GetAsync(Friend, "w1")).Name);
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.RestartAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task Operate_can_stop_but_not_edit_the_config()
    {
        var (svc, _, backend) = Build(Container(), [Grant(AccessLevel.Operate)]);
        backend.Setup(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await svc.StopAsync(Friend, "w1");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() =>
            svc.UpdateAsync(Friend, "w1", new UpdateWorkloadRequest { Image = "nginx:1.28" }));
    }

    [TestMethod]
    public async Task Moving_a_workload_needs_configure_not_operate()
    {
        var native = new Workload
        {
            Id = "w1", Name = "srcds", Target = WorkloadTarget.Agent, Kind = WorkloadKind.Native,
            AgentId = "a1", Command = "/srv/srcds",
        };
        var (svc, _, _) = Build(native, [Grant(AccessLevel.Operate)]);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.MoveAsync(Friend, "w1", "a2"));
    }

    [TestMethod]
    public async Task Operate_cannot_deploy_deploying_publishes_config()
    {
        var workload = Container();
        var (svc, _, backend) = Build(workload, [Grant(AccessLevel.Operate)]);
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // revision 1 is what's running and the saved config still matches it
        await svc.DeployAsync(Admin, "w1");
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeployAsync(Friend, "w1"));

        // and certainly not with someone else's edit sitting unpublished
        workload.Image = "nginx:1.28";
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeployAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task Secret_values_only_come_back_for_someone_who_can_edit_them()
    {
        var workload = Container();
        workload.Env = [new EnvVar("RCON_PASSWORD", "enc:hunter2", Secret: true)];

        workload.Env = [.. workload.Env, new EnvVar("PLAIN", "yes")];

        // no env at all below Configure, the Secret flag is a human decision and the one nobody ticked would leak
        var (operate, _, _) = Build(workload, [Grant(AccessLevel.Operate)]);
        Assert.IsEmpty((await operate.GetAsync(Friend, "w1")).Env);

        var (configure, _, _) = Build(workload, [Grant(AccessLevel.Configure)]);
        var env = (await configure.GetAsync(Friend, "w1")).Env;
        Assert.AreEqual("hunter2", env.Single(v => v.Key == "RCON_PASSWORD").Value);
        Assert.AreEqual("yes", env.Single(v => v.Key == "PLAIN").Value);
    }

    [TestMethod]
    public async Task History_keeps_env_out_of_reach_below_configure()
    {
        var workload = Container();
        workload.Env = [new EnvVar("RCON_PASSWORD", "enc:hunter2", Secret: true), new EnvVar("PLAIN", "yes")];

        var (operate, _, backend) = Build(workload, [Grant(AccessLevel.Operate)]);
        backend.Setup(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await operate.DeployAsync(Admin, "w1");

        var seenByOperate = await operate.GetRevisionsAsync(Friend, "w1");
        Assert.IsEmpty(seenByOperate.Single().Env);

        var (configure, _, _) = Build(workload, [Grant(AccessLevel.Configure)]);
        var seenByConfigure = (await configure.GetRevisionsAsync(Friend, "w1")).Single().Env;
        Assert.AreEqual("yes", seenByConfigure.Single(v => v.Key == "PLAIN").Value);
        Assert.AreEqual(string.Empty, seenByConfigure.Single(v => v.Key == "RCON_PASSWORD").Value, "secrets stay blank even for Configure");
    }

    [TestMethod]
    public async Task Creating_and_deleting_stay_with_the_administrator()
    {
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.Configure)]);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() =>
            svc.CreateAsync(Friend, new CreateWorkloadRequest { Name = "new", Image = "nginx" }));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeleteAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task Listing_shows_only_what_you_can_reach()
    {
        var theirs = new Workload { Id = "w2", Name = "db", Kind = WorkloadKind.Container, Image = "postgres:17" };
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.Operate)], others: [theirs]);

        var mine = await svc.ListAsync(Friend);

        Assert.AreEqual("web", mine.Single().Name);
        Assert.HasCount(2, await svc.ListAsync(Admin));
    }

    [TestMethod]
    public async Task The_response_carries_the_level_the_server_enforced()
    {
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.Operate)]);

        Assert.AreEqual(AccessLevel.Operate, (await svc.GetAsync(Friend, "w1")).Access);
        Assert.AreEqual(AccessLevel.Operate, (await svc.ListAsync(Friend)).Single().Access);
        Assert.AreEqual(AccessLevel.Configure, (await svc.GetAsync(Admin, "w1")).Access);
    }

    [TestMethod]
    public async Task The_status_map_is_filtered_the_same_way_as_the_list()
    {
        var theirs = new Workload { Id = "w2", Name = "db", Kind = WorkloadKind.Container, Image = "postgres:17" };
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.View)], others: [theirs]);

        var statuses = await svc.GetStatusesAsync(Friend);

        // keys are workload ids, unfiltered would leak the whole inventory
        Assert.AreEqual("w1", statuses.Keys.Single());
    }

    [TestMethod]
    public async Task Routes_come_back_scoped_to_the_one_workload()
    {
        var routes = new List<RouteResponse>
        {
            new() { Id = "r1", Hostname = "acme.example.com", Path = "/", WorkloadId = "w1" },
            new() { Id = "r2", Hostname = "other.example.com", Path = "/", WorkloadId = "w2" },
        };
        var (svc, _, _) = Build(Container(), [Grant(AccessLevel.View)], routes: routes);

        // listing all routes is admin-only, this is how a granted user sees their address
        Assert.AreEqual("r1", (await svc.GetRoutesAsync(Friend, "w1")).Single().Id);
    }

    [TestMethod]
    public async Task Routes_for_a_workload_you_cannot_see_are_a_not_found()
    {
        var (svc, _, _) = Build(Container());

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.GetRoutesAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task Deleting_a_workload_takes_its_grants_with_it()
    {
        var (svc, grants, _) = Build(Container(), [Grant(AccessLevel.Configure)]);

        await svc.DeleteAsync(Admin, "w1");

        grants.Verify(g => g.RemoveForTargetAsync(AccessScope.Workload, "w1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AccessGrant TeamGrant(AccessLevel level, AccessScope scope = AccessScope.Workload, string target = "w1")
        => new() { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = scope, TargetId = target, Level = level };

    private static Team Ops(params string[] members) => new() { Id = "t1", Name = "ops", MemberIds = [.. members] };

    [TestMethod]
    public async Task A_team_grant_reaches_a_member()
    {
        var (svc, _, backend) = Build(Container(), [TeamGrant(AccessLevel.Operate)], null, null, null, Ops("u1"));
        backend.Setup(b => b.StopAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Assert.AreEqual("web", (await svc.GetAsync(Friend, "w1")).Name);
        await svc.StopAsync(Friend, "w1");
    }

    [TestMethod]
    public async Task A_team_you_are_not_in_gives_you_nothing()
    {
        var (svc, _, _) = Build(Container(), [TeamGrant(AccessLevel.Configure)], null, null, null, Ops("someone-else"));

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.GetAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task A_team_grant_on_an_ancestor_group_reaches_a_nested_workload()
    {
        var groups = new List<WorkloadGroup>
        {
            new() { Id = "clients", Name = "clients" },
            new() { Id = "acme", Name = "acme", ParentId = "clients" },
        };
        var (svc, _, _) = Build(
            Container(groupId: "acme"),
            [TeamGrant(AccessLevel.View, AccessScope.Group, "clients")],
            groups, null, null, Ops("u1"));

        Assert.AreEqual("web", (await svc.GetAsync(Friend, "w1")).Name);
    }

    [TestMethod]
    public async Task The_higher_of_a_personal_and_a_team_grant_wins()
    {
        // personal View, team Configure: the team raises it, the personal grant doesn't cap it
        var (svc, _, _) = Build(
            Container(),
            [Grant(AccessLevel.View), TeamGrant(AccessLevel.Configure)],
            null, null, null, Ops("u1"));

        await svc.UpdateAsync(Friend, "w1", new UpdateWorkloadRequest { Image = "nginx:1.28", Replicas = 1 });
    }

    [TestMethod]
    public async Task A_personal_grant_still_wins_when_it_is_the_higher_one()
    {
        var (svc, _, _) = Build(
            Container(),
            [Grant(AccessLevel.Configure), TeamGrant(AccessLevel.View)],
            null, null, null, Ops("u1"));

        await svc.UpdateAsync(Friend, "w1", new UpdateWorkloadRequest { Image = "nginx:1.28", Replicas = 1 });
    }
}
