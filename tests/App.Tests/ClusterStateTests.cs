using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class ClusterStateTests
{
    private static NodeResponse Node(string id, NodeStatus status = NodeStatus.Ready) => new()
    {
        Id = id,
        Hostname = id,
        Status = status,
    };

    private static WorkloadRuntimeStatus Status(WorkloadState state, int running = 1) => new()
    {
        Name = "web",
        Deployed = true,
        DesiredReplicas = 1,
        RunningReplicas = running,
        State = state,
    };

    [TestMethod]
    public void Nothing_has_been_observed_before_the_first_write()
    {
        var state = new ClusterState();

        Assert.IsFalse(state.Hydrated);
        Assert.IsEmpty(state.Nodes);
    }

    [TestMethod]
    public void The_first_write_counts_as_a_change_and_hydrates()
    {
        var state = new ClusterState();

        Assert.IsTrue(state.SetNodes([Node("a")]));
        Assert.IsTrue(state.Hydrated);
        Assert.HasCount(1, state.Nodes);
    }

    [TestMethod]
    public void A_cluster_with_no_nodes_still_counts_as_observed()
    {
        // otherwise every read re-fans-out forever against a swarm that really is empty
        var state = new ClusterState();

        Assert.IsTrue(state.SetNodes([]));
        Assert.IsTrue(state.Hydrated);
    }

    [TestMethod]
    public void Writing_the_same_nodes_again_is_not_a_change()
    {
        var state = new ClusterState();
        state.SetNodes([Node("a"), Node("b")]);

        Assert.IsFalse(state.SetNodes([Node("a"), Node("b")]));
    }

    [TestMethod]
    public void A_node_changing_status_is_a_change()
    {
        var state = new ClusterState();
        state.SetNodes([Node("a")]);

        Assert.IsTrue(state.SetNodes([Node("a", NodeStatus.Down)]));
        Assert.AreEqual(NodeStatus.Down, state.Nodes[0].Status);
    }

    [TestMethod]
    public void An_unknown_workload_has_no_status()
        => Assert.IsNull(new ClusterState().StatusFor("nope"));

    [TestMethod]
    public void The_first_status_for_a_workload_is_a_change()
    {
        var state = new ClusterState();

        Assert.IsTrue(state.SetWorkloadStatus("w1", Status(WorkloadState.Running)));
        Assert.AreEqual(WorkloadState.Running, state.StatusFor("w1")!.State);
    }

    [TestMethod]
    public void Re_reporting_an_identical_status_is_not_a_change()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status(WorkloadState.Running));

        Assert.IsFalse(state.SetWorkloadStatus("w1", Status(WorkloadState.Running)));
    }

    [TestMethod]
    public void A_workload_changing_state_is_a_change()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status(WorkloadState.Running));

        Assert.IsTrue(state.SetWorkloadStatus("w1", Status(WorkloadState.Partial, running: 0)));
        Assert.AreEqual(WorkloadState.Partial, state.StatusFor("w1")!.State);
    }

    [TestMethod]
    public void Workloads_are_tracked_separately()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status(WorkloadState.Running));
        state.SetWorkloadStatus("w2", Status(WorkloadState.Stopped));

        Assert.AreEqual(WorkloadState.Running, state.StatusFor("w1")!.State);
        Assert.AreEqual(WorkloadState.Stopped, state.StatusFor("w2")!.State);
    }

    [TestMethod]
    public void Forgetting_a_workload_drops_its_status()
    {
        var state = new ClusterState();
        state.SetWorkloadStatus("w1", Status(WorkloadState.Running));

        state.ForgetWorkload("w1");

        Assert.IsNull(state.StatusFor("w1"));
    }

    [TestMethod]
    public void Forgetting_a_workload_that_was_never_there_is_harmless()
        => new ClusterState().ForgetWorkload("nope");
}
