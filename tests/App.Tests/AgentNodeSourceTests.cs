using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Nodes;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AgentNodeSourceTests
{
    private static AgentNodeSource Build(Agent agent, bool online)
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent> { agent });
        var registry = new Mock<IAgentRegistry>();
        registry.Setup(r => r.IsOnline(agent.Id)).Returns(online);
        return new AgentNodeSource(agents.Object, registry.Object);
    }

    [TestMethod]
    public async Task Online_windows_agent_maps_to_a_ready_agent_node()
    {
        var source = Build(new Agent { Id = "a1", Name = "win-1", Platform = AgentPlatform.Windows }, online: true);

        var node = (await source.GetNodesAsync()).Single();

        Assert.AreEqual("a1", node.Id);
        Assert.AreEqual("win-1", node.Hostname);
        Assert.AreEqual(NodeStatus.Ready, node.Status);
        Assert.AreEqual(NodePlatform.Windows, node.Platform);
        Assert.AreEqual(NodeBackendKind.Agent, node.Backend);
    }

    [TestMethod]
    public async Task Offline_agent_maps_to_down()
    {
        var source = Build(new Agent { Id = "a1", Name = "n", Platform = AgentPlatform.Linux }, online: false);

        var node = (await source.GetNodesAsync()).Single();

        Assert.AreEqual(NodeStatus.Down, node.Status);
        Assert.AreEqual(NodePlatform.Linux, node.Platform);
    }

    [TestMethod]
    public async Task Maps_a_macos_agent_rather_than_reporting_unknown()
    {
        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent> { new() { Id = "a1", Name = "mac-1", Platform = AgentPlatform.MacOS } });

        var nodes = await new AgentNodeSource(agents.Object, new Mock<IAgentRegistry>().Object).GetNodesAsync();

        Assert.AreEqual(NodePlatform.MacOS, nodes.Single().Platform);
    }
}
