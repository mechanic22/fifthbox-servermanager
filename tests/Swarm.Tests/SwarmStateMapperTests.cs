using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Cluster;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

[TestClass]
public class SwarmStateMapperTests
{
    [TestMethod]
    public void Maps_an_active_manager()
    {
        var state = SwarmStateMapper.ToState(new DockerModels.Info
        {
            LocalNodeState = "active",
            ControlAvailable = true,
            NodeID = "n1",
            NodeAddr = "10.0.0.5",
            Nodes = 3,
            Managers = 1,
        });

        Assert.AreEqual(SwarmMembership.Active, state.Membership);
        Assert.IsTrue(state.IsInSwarm);
        Assert.IsTrue(state.IsManager);
        Assert.AreEqual("n1", state.NodeId);
        Assert.AreEqual("10.0.0.5", state.NodeAddress);
        Assert.AreEqual(3, state.NodeCount);
        Assert.AreEqual(1, state.ManagerCount);
        Assert.IsNull(state.Error);
    }

    [TestMethod]
    public void Maps_inactive_as_not_in_swarm()
    {
        var state = SwarmStateMapper.ToState(new DockerModels.Info { LocalNodeState = "inactive", ControlAvailable = false });

        Assert.AreEqual(SwarmMembership.Inactive, state.Membership);
        Assert.IsFalse(state.IsInSwarm);
        Assert.IsFalse(state.IsManager);
        Assert.IsNull(state.NodeId);
    }

    [TestMethod]
    public void Maps_error_state_with_message()
    {
        var state = SwarmStateMapper.ToState(new DockerModels.Info { LocalNodeState = "error", Error = "boom" });

        Assert.AreEqual(SwarmMembership.Error, state.Membership);
        Assert.AreEqual("boom", state.Error);
    }

    [TestMethod]
    public void Unknown_state_falls_back_to_Unknown()
    {
        var state = SwarmStateMapper.ToState(new DockerModels.Info { LocalNodeState = "weird" });

        Assert.AreEqual(SwarmMembership.Unknown, state.Membership);
    }
}
