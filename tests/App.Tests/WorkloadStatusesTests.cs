using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

/// The list page asks for every status at once. Observed state answers for free; only what nobody has
/// seen costs a backend call.
[TestClass]
public class WorkloadStatusesTests
{
    private static readonly Caller Admin = new("admin", IsAdmin: true);

    private static Workload Container(string id) => new()
    {
        Id = id,
        Name = id,
        Target = WorkloadTarget.Swarm,
        Kind = WorkloadKind.Container,
        Image = "nginx:latest",
    };

    private static WorkloadRuntimeStatus Status(string name, WorkloadState state) => new()
    {
        Name = name,
        Deployed = state != WorkloadState.NotDeployed,
        DesiredReplicas = 1,
        RunningReplicas = state == WorkloadState.Running ? 1 : 0,
        State = state,
    };

    private static (WorkloadService Service, Mock<IWorkloadBackend> Backend) Build(
        ClusterState state,
        params Workload[] workloads)
    {
        var repo = new Mock<IWorkloadRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(workloads);

        var backend = new Mock<IWorkloadBackend>();
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns(backend.Object);

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent>());
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<WorkloadGroup>());
        var nodes = new Mock<INodeService>();
        nodes.Setup(n => n.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NodeResponse>());
        var settings = new Mock<IPlatformSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformSettings());

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var protector = new Mock<ISecretProtector>().Object;
        var cluster = Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" });
        var access = new WorkloadAccess(grants.Object, groups.Object, TeamRepo.None());
        var loader = new WorkloadLoader(repo.Object, access);
        var factory = new WorkloadDeploymentFactory(protector, cluster);
        var lifecycle = new WorkloadLifecycleService(
            loader, repo.Object, access, resolver.Object, new Mock<IDeployedSpecSource>().Object,
            state, factory, TimeProvider.System);

        var service = new WorkloadService(
            repo.Object,
            loader,
            lifecycle,
            agents.Object,
            new Mock<IAgentRegistry>().Object,
            groups.Object,
            access,
            grants.Object,
            resolver.Object,
            new Mock<IRouteService>().Object,
            settings.Object,
            nodes.Object,
            protector,
            factory,
            TimeProvider.System);

        return (service, backend);
    }

    [TestMethod]
    public async Task An_observed_status_is_answered_without_touching_the_backend()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status("w1", WorkloadState.Running));
        var (service, backend) = Build(state, Container("w1"));

        var statuses = await service.GetStatusesAsync(Admin);

        Assert.AreEqual(WorkloadState.Running, statuses["w1"].State);
        backend.Verify(
            b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task An_unobserved_workload_is_fetched_once_and_remembered()
    {
        var state = new ClusterState();
        var (service, backend) = Build(state, Container("w1"));
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status("w1", WorkloadState.Running));

        var first = await service.GetStatusesAsync(Admin);
        var second = await service.GetStatusesAsync(Admin);

        Assert.AreEqual(WorkloadState.Running, first["w1"].State);
        Assert.AreEqual(WorkloadState.Running, second["w1"].State);
        Assert.AreEqual(WorkloadState.Running, state.StatusFor("w1")!.State);
        backend.Verify(
            b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task One_unreachable_backend_does_not_blank_the_others()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status("w1", WorkloadState.Running));
        var (service, backend) = Build(state, Container("w1"), Container("w2"));
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("docker is not reachable"));

        var statuses = await service.GetStatusesAsync(Admin);

        Assert.HasCount(1, statuses);
        Assert.AreEqual(WorkloadState.Running, statuses["w1"].State);
    }

    [TestMethod]
    public async Task No_workloads_is_an_empty_map_rather_than_an_error()
        => Assert.IsEmpty(await Build(new ClusterState()).Service.GetStatusesAsync(Admin));
}
