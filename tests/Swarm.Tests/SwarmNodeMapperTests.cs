using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Nodes;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

[TestClass]
public class SwarmNodeMapperTests
{
    [TestMethod]
    public void Maps_a_ready_active_manager_leader()
    {
        var node = SwarmNodeMapper.ToNode(new DockerModels.NodeListResponse
        {
            ID = "abc123",
            Spec = new DockerModels.NodeUpdateParameters { Role = "manager", Availability = "active" },
            Description = new DockerModels.NodeDescription
            {
                Hostname = "host-1",
                Platform = new DockerModels.Platform { OS = "linux", Architecture = "x86_64" },
                Engine = new DockerModels.EngineDescription { EngineVersion = "27.0.1" },
            },
            Status = new DockerModels.NodeStatus { State = "ready", Addr = "10.0.0.1" },
            ManagerStatus = new DockerModels.ManagerStatus { Leader = true },
        });

        Assert.AreEqual("abc123", node.Id);
        Assert.AreEqual("host-1", node.Hostname);
        Assert.AreEqual(NodeRole.Manager, node.Role);
        Assert.AreEqual(NodeStatus.Ready, node.Status);
        Assert.AreEqual(NodeAvailability.Active, node.Availability);
        Assert.AreEqual(NodePlatform.Linux, node.Platform);
        Assert.AreEqual("x86_64", node.Architecture);
        Assert.IsTrue(node.IsLeader);
        Assert.AreEqual("27.0.1", node.EngineVersion);
        Assert.AreEqual("10.0.0.1", node.Address);
        Assert.AreEqual(NodeBackendKind.Swarm, node.Backend);
    }

    [TestMethod]
    public void Maps_a_drained_down_worker()
    {
        var node = SwarmNodeMapper.ToNode(new DockerModels.NodeListResponse
        {
            ID = "w1",
            Spec = new DockerModels.NodeUpdateParameters { Role = "worker", Availability = "drain" },
            Description = new DockerModels.NodeDescription
            {
                Hostname = "worker-1",
                Platform = new DockerModels.Platform { OS = "windows", Architecture = "x86_64" },
            },
            Status = new DockerModels.NodeStatus { State = "down" },
            ManagerStatus = null,
        });

        Assert.AreEqual(NodeRole.Worker, node.Role);
        Assert.AreEqual(NodeStatus.Down, node.Status);
        Assert.AreEqual(NodeAvailability.Drain, node.Availability);
        Assert.AreEqual(NodePlatform.Windows, node.Platform);
        Assert.IsFalse(node.IsLeader);
    }

    [TestMethod]
    public void Unknown_or_missing_values_fall_back_to_Unknown()
    {
        var node = SwarmNodeMapper.ToNode(new DockerModels.NodeListResponse
        {
            ID = "x",
            Spec = new DockerModels.NodeUpdateParameters { Role = "worker", Availability = "something-new" },
            Description = new DockerModels.NodeDescription { Hostname = "h", Platform = new DockerModels.Platform { OS = "" } },
            Status = new DockerModels.NodeStatus { State = "weird" },
        });

        Assert.AreEqual(NodeStatus.Unknown, node.Status);
        Assert.AreEqual(NodeAvailability.Unknown, node.Availability);
        Assert.AreEqual(NodePlatform.Unknown, node.Platform);
        Assert.IsNull(node.Architecture);
        Assert.IsNull(node.EngineVersion);
        Assert.IsNull(node.Address);
    }

    [TestMethod]
    public void Role_parsing_is_case_insensitive()
    {
        var node = SwarmNodeMapper.ToNode(new DockerModels.NodeListResponse
        {
            ID = "m",
            Spec = new DockerModels.NodeUpdateParameters { Role = "Manager", Availability = "Active" },
            Description = new DockerModels.NodeDescription { Hostname = "h" },
            Status = new DockerModels.NodeStatus { State = "Ready" },
        });

        Assert.AreEqual(NodeRole.Manager, node.Role);
        Assert.AreEqual(NodeAvailability.Active, node.Availability);
        Assert.AreEqual(NodeStatus.Ready, node.Status);
    }
}
