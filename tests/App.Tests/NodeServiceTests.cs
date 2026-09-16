using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Nodes;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class NodeServiceTests
{
    private static NodeService Build(
        IEnumerable<INodeSource> sources,
        Mock<INodeControl>? control = null,
        string? localNodeId = null)
    {
        var swarm = new Mock<ISwarmLifecycle>();
        swarm.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwarmState { IsInSwarm = true, IsManager = true, NodeId = localNodeId });

        return new NodeService(sources, new ClusterState(), (control ?? new Mock<INodeControl>()).Object, swarm.Object);
    }

    private static Node Swarm(string id, NodeRole role = NodeRole.Worker, NodeStatus status = NodeStatus.Ready) => new()
    {
        Id = id,
        Hostname = id,
        Backend = NodeBackendKind.Swarm,
        Role = role,
        Status = status,
        Availability = NodeAvailability.Active,
    };

    private static INodeSource Source(params Node[] nodes)
    {
        var source = new Mock<INodeSource>();
        source.Setup(s => s.GetNodesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(nodes);
        return source.Object;
    }

    private static INodeSource FailingSource()
    {
        var source = new Mock<INodeSource>();
        source.Setup(s => s.GetNodesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("This node is not a swarm manager"));
        return source.Object;
    }

    private static Node Agent(string id) => new()
    {
        Id = id,
        Hostname = id,
        Backend = NodeBackendKind.Agent,
        Status = NodeStatus.Ready,
    };

    [TestMethod]
    public async Task A_source_that_cannot_answer_does_not_empty_the_list()
    {
        // the swarm source always throws before bootstrap, agents still have to show up
        var svc = Build([FailingSource(), Source(Agent("agent-1"))]);

        var nodes = await svc.ListAsync();

        Assert.HasCount(1, nodes);
        Assert.AreEqual("agent-1", nodes[0].Id);
    }

    [TestMethod]
    public async Task Every_source_contributes_to_one_list()
    {
        var svc = Build([Source(Agent("a")), Source(Agent("b"))]);

        Assert.HasCount(2, await svc.ListAsync());
    }

    [TestMethod]
    public async Task All_sources_failing_is_an_empty_list_rather_than_an_error()
    {
        var svc = Build([FailingSource()]);

        Assert.IsEmpty(await svc.ListAsync());
    }

    [TestMethod]
    public async Task Draining_a_node_goes_through_to_the_backend()
    {
        var control = new Mock<INodeControl>();
        var svc = Build([Source(Swarm("n1"))], control);

        await svc.SetAvailabilityAsync("n1", NodeAvailability.Drain);

        control.Verify(c => c.SetAvailabilityAsync("n1", NodeAvailability.Drain, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task An_agent_has_no_schedulability_to_set()
    {
        var svc = Build([Source(Agent("a1"))]);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.SetAvailabilityAsync("a1", NodeAvailability.Drain));
    }

    [TestMethod]
    public async Task The_last_manager_cannot_be_demoted()
    {
        var svc = Build([Source(Swarm("n1", NodeRole.Manager), Swarm("n2"))], localNodeId: "n2");

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.SetRoleAsync("n1", NodeRole.Worker));

        StringAssert.Contains(ex.Message, "at least one manager");
    }

    [TestMethod]
    public async Task The_node_we_reach_docker_through_cannot_be_demoted()
    {
        // demoting it leaves ServerManager talking to a worker, which can't answer manager calls
        var svc = Build([Source(Swarm("n1", NodeRole.Manager), Swarm("n2", NodeRole.Manager))], localNodeId: "n1");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.SetRoleAsync("n1", NodeRole.Worker));
    }

    [TestMethod]
    public async Task A_second_manager_can_be_demoted()
    {
        var control = new Mock<INodeControl>();
        var svc = Build([Source(Swarm("n1", NodeRole.Manager), Swarm("n2", NodeRole.Manager))], control, localNodeId: "n1");

        await svc.SetRoleAsync("n2", NodeRole.Worker);

        control.Verify(c => c.SetRoleAsync("n2", NodeRole.Worker, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task A_reachable_node_is_not_removed()
    {
        // docker node rm on a live node needs --force and it still thinks it's a member
        var svc = Build([Source(Swarm("n1"))]);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.RemoveAsync("n1"));
    }

    [TestMethod]
    public async Task A_node_that_is_down_can_be_removed()
    {
        var control = new Mock<INodeControl>();
        var svc = Build([Source(Swarm("n1", status: NodeStatus.Down))], control, localNodeId: "n2");

        await svc.RemoveAsync("n1");

        control.Verify(c => c.RemoveAsync("n1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
