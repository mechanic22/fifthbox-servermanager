using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Integrations.Agents;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.Integrations.Agents.Tests;

[TestClass]
public class AgentBackendTests
{
    private static WorkloadDeployment Deployment(string? agentId = "ag1") => new()
    {
        Name = "worker",
        Kind = WorkloadKind.Native,
        AgentId = agentId,
        Command = "dotnet",
        Args = ["run"],
    };

    [TestMethod]
    public async Task SendConsole_forwards_the_text_to_the_agent()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.SendConsoleAsync("ag1", "worker", "save", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Running = true });

        await new AgentBackend(channel.Object).SendConsoleAsync(Deployment(), "save");

        channel.Verify(c => c.SendConsoleAsync("ag1", "worker", "save", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendConsole_throws_conflict_when_nothing_was_listening()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.SendConsoleAsync("ag1", "worker", "save", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Running = false, Detail = "not running here" });

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(
            () => new AgentBackend(channel.Object).SendConsoleAsync(Deployment(), "save"));

        StringAssert.Contains(ex.Message, "not running here");
    }

    [TestMethod]
    public async Task Update_forwards_the_spec_to_the_agent()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.UpdateAsync("ag1", It.IsAny<AgentWorkloadSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Updating = true });

        await new AgentBackend(channel.Object).UpdateAsync(Deployment() with { Source = new WorkloadSourceSpec { Kind = SourceKind.Zip, Url = "https://example.com/s.zip" } });

        channel.Verify(c => c.UpdateAsync("ag1", It.Is<AgentWorkloadSpec>(s => s.Source!.Url == "https://example.com/s.zip"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void SupportedKind_is_native()
    {
        Assert.AreEqual(WorkloadKind.Native, new AgentBackend(Mock.Of<IAgentCommandChannel>()).SupportedKind);
    }

    [TestMethod]
    public async Task GetStatus_reports_not_deployed_when_agent_is_offline()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.IsConnected("ag1")).Returns(false);

        var status = await new AgentBackend(channel.Object).GetStatusAsync(Deployment());

        Assert.IsFalse(status.Deployed);
        Assert.AreEqual(WorkloadState.NotDeployed, status.State);
        channel.Verify(c => c.GetStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetStatus_maps_a_running_process_to_running()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.IsConnected("ag1")).Returns(true);
        channel.Setup(c => c.GetStatusAsync("ag1", "worker", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Running = true });

        var status = await new AgentBackend(channel.Object).GetStatusAsync(Deployment());

        Assert.IsTrue(status.Deployed);
        Assert.AreEqual(WorkloadState.Running, status.State);
        Assert.AreEqual(1, status.RunningReplicas);
    }

    [TestMethod]
    public async Task GetStatus_maps_a_stopped_process_to_stopped()
    {
        var channel = new Mock<IAgentCommandChannel>();
        channel.Setup(c => c.IsConnected("ag1")).Returns(true);
        channel.Setup(c => c.GetStatusAsync("ag1", "worker", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Running = false });

        var status = await new AgentBackend(channel.Object).GetStatusAsync(Deployment());

        Assert.AreEqual(WorkloadState.Stopped, status.State);
        Assert.AreEqual(0, status.RunningReplicas);
    }

    [TestMethod]
    public async Task Deploy_without_an_agent_throws_validation()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            new AgentBackend(Mock.Of<IAgentCommandChannel>()).DeployAsync(Deployment(agentId: null)));
    }

    [TestMethod]
    public async Task Deploy_forwards_the_spec_to_the_agent()
    {
        var channel = new Mock<IAgentCommandChannel>();
        AgentWorkloadSpec? sent = null;
        channel.Setup(c => c.DeployAsync("ag1", It.IsAny<AgentWorkloadSpec>(), It.IsAny<CancellationToken>()))
            .Callback<string, AgentWorkloadSpec, CancellationToken>((_, s, _) => sent = s)
            .ReturnsAsync(new AgentWorkloadStatus { Name = "worker", Running = true });

        await new AgentBackend(channel.Object).DeployAsync(Deployment());

        Assert.AreEqual("worker", sent!.Name);
        Assert.AreEqual("dotnet", sent.Command);
        Assert.HasCount(1, sent.Args);
    }
}
