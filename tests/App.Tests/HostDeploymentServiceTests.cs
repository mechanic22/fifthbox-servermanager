using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class HostDeploymentServiceTests
{
    private static readonly HostDeploymentOptions Defaults = new();

    private static HostDeploymentService Build(
        string? swarmServiceName,
        bool isContainer,
        bool managedExists,
        string? localNodeId = "node1")
    {
        var process = new Mock<IHostProcessInfo>();
        process.SetupGet(p => p.SwarmServiceName).Returns(swarmServiceName);
        process.SetupGet(p => p.IsContainer).Returns(isContainer);

        var platform = new Mock<IPlatformServices>();
        platform.Setup(p => p.ServiceExistsAsync(Defaults.ServiceName, It.IsAny<CancellationToken>())).ReturnsAsync(managedExists);
        platform.Setup(p => p.LocalNodeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(localNodeId);

        return new HostDeploymentService(process.Object, platform.Object, Custody(), Options.Create(Defaults), Options.Create(new EncryptionOptions()));
    }

    private static ISecretCustodyService Custody()
    {
        var custody = new Mock<ISecretCustodyService>();
        custody.Setup(c => c.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SecretsState.NoSecrets);
        return custody.Object;
    }

    [TestMethod]
    public async Task Bare_process_when_neither_signal_is_present()
    {
        var info = await Build(swarmServiceName: null, isContainer: false, managedExists: false).GetAsync();

        Assert.AreEqual(HostRunMode.BareProcess, info.RunMode);
    }

    [TestMethod]
    public async Task Container_when_dockerenv_exists_but_no_service_name()
    {
        var info = await Build(swarmServiceName: null, isContainer: true, managedExists: false).GetAsync();

        Assert.AreEqual(HostRunMode.Container, info.RunMode);
    }

    [TestMethod]
    public async Task Swarm_service_when_the_templated_env_var_is_set()
    {
        var info = await Build(swarmServiceName: "fbsm-host", isContainer: true, managedExists: true).GetAsync();

        Assert.AreEqual(HostRunMode.SwarmService, info.RunMode);
        Assert.AreEqual("fbsm-host", info.ServiceName);
    }

    [TestMethod]
    public async Task Managed_install_is_not_split_brain()
    {
        var info = await Build(swarmServiceName: "fbsm-host", isContainer: true, managedExists: true).GetAsync();

        Assert.IsFalse(info.SplitBrain);
    }

    [TestMethod]
    public async Task Unmanaged_alongside_an_existing_service_is_split_brain()
    {
        var info = await Build(swarmServiceName: null, isContainer: true, managedExists: true).GetAsync();

        Assert.IsTrue(info.SplitBrain, "two Hosts would share one Docker socket and one database");
    }

    [TestMethod]
    public async Task Unmanaged_with_no_service_is_not_split_brain()
    {
        var info = await Build(swarmServiceName: null, isContainer: true, managedExists: false).GetAsync();

        Assert.IsFalse(info.SplitBrain);
    }

    [TestMethod]
    public async Task A_missing_swarm_still_produces_an_install_command()
    {
        var platform = new Mock<IPlatformServices>();
        platform.Setup(p => p.ServiceExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        platform.Setup(p => p.LocalNodeIdAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not in a swarm"));
        var process = new Mock<IHostProcessInfo>();
        process.SetupGet(p => p.IsContainer).Returns(true);

        var info = await new HostDeploymentService(
            process.Object, platform.Object, Custody(), Options.Create(Defaults), Options.Create(new EncryptionOptions())).GetAsync();

        StringAssert.Contains(info.InstallCommand, "node.role==manager", "falls back when the node id can't be resolved");
    }
}

[TestClass]
public class InstallCommandRendererTests
{
    private static readonly HostDeploymentOptions Defaults = new();

    [TestMethod]
    public void Removes_the_unmanaged_container_before_creating_the_service()
    {
        var command = InstallCommandRenderer.Render(Defaults, "node1");

        var remove = command.IndexOf("docker rm -f", StringComparison.Ordinal);
        var create = command.IndexOf("docker service create", StringComparison.Ordinal);
        Assert.IsTrue(remove >= 0 && create > remove, "stop first — both instances would otherwise share the SQLite file");
    }

    [TestMethod]
    public void Pins_to_the_resolved_node_so_the_volume_is_reachable()
    {
        StringAssert.Contains(InstallCommandRenderer.Render(Defaults, "abc123"), "node.id==abc123");
    }

    [TestMethod]
    public void Falls_back_to_a_manager_constraint_without_a_node_id()
    {
        var command = InstallCommandRenderer.Render(Defaults, null);

        StringAssert.Contains(command, "node.role==manager");
        Assert.IsFalse(command.Contains("node.id==", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Reuses_the_same_named_volume_so_the_database_survives_the_handover()
    {
        StringAssert.Contains(InstallCommandRenderer.Render(Defaults, "node1"),
            $"type=volume,src={Defaults.DataVolume},dst={Defaults.DataPath}");
    }

    [TestMethod]
    public void Sets_the_templated_service_name_that_marks_a_managed_install()
    {
        StringAssert.Contains(InstallCommandRenderer.Render(Defaults, "node1"), "FBSM_SERVICE_NAME='{{.Service.Name}}'");
    }

    [TestMethod]
    public void Stamps_the_platform_label()
    {
        StringAssert.Contains(InstallCommandRenderer.Render(Defaults, "node1"),
            $"{PlatformLabels.RoleKey}={PlatformLabels.PlatformRole}");
    }

    [TestMethod]
    public void Emits_placeholders_not_real_secrets()
    {
        var command = InstallCommandRenderer.Render(Defaults, "node1");

        StringAssert.Contains(command, "<your-encryption-key>");
        StringAssert.Contains(command, "<your-jwt-secret-key>");
    }
}
