using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Cluster;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class ClusterServiceTests
{
    private const string Network = "test-net";

    private static ClusterService Build(Mock<ISwarmLifecycle> lifecycle) =>
        new(lifecycle.Object, Options.Create(new ClusterOptions { OverlayNetwork = Network }));

    private static SwarmState State(bool inSwarm, bool manager = true) => new()
    {
        Membership = inSwarm ? SwarmMembership.Active : SwarmMembership.Inactive,
        IsInSwarm = inSwarm,
        IsManager = manager,
    };

    [TestMethod]
    public async Task Bootstrap_when_not_in_swarm_inits_and_creates_network()
    {
        var lifecycle = new Mock<ISwarmLifecycle>();
        lifecycle.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(inSwarm: false));
        lifecycle.Setup(l => l.NetworkExistsAsync(Network, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Build(lifecycle).BootstrapAsync();

        lifecycle.Verify(l => l.InitSwarmAsync(It.IsAny<CancellationToken>()), Times.Once);
        lifecycle.Verify(l => l.CreateOverlayNetworkAsync(Network, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Bootstrap_when_already_set_up_is_a_no_op()
    {
        var lifecycle = new Mock<ISwarmLifecycle>();
        lifecycle.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(inSwarm: true));
        lifecycle.Setup(l => l.NetworkExistsAsync(Network, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Build(lifecycle).BootstrapAsync();

        lifecycle.Verify(l => l.InitSwarmAsync(It.IsAny<CancellationToken>()), Times.Never);
        lifecycle.Verify(l => l.CreateOverlayNetworkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Bootstrap_in_swarm_but_missing_network_only_creates_network()
    {
        var lifecycle = new Mock<ISwarmLifecycle>();
        lifecycle.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(inSwarm: true));
        lifecycle.Setup(l => l.NetworkExistsAsync(Network, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Build(lifecycle).BootstrapAsync();

        lifecycle.Verify(l => l.InitSwarmAsync(It.IsAny<CancellationToken>()), Times.Never);
        lifecycle.Verify(l => l.CreateOverlayNetworkAsync(Network, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Join_info_unavailable_when_not_a_manager()
    {
        var lifecycle = new Mock<ISwarmLifecycle>();
        lifecycle.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(inSwarm: true, manager: false));

        var join = await Build(lifecycle).GetJoinInfoAsync();

        Assert.IsFalse(join.Available);
        lifecycle.Verify(l => l.GetJoinTokensAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Join_info_builds_worker_command_when_manager()
    {
        var lifecycle = new Mock<ISwarmLifecycle>();
        lifecycle.Setup(l => l.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(inSwarm: true, manager: true));
        lifecycle.Setup(l => l.GetJoinTokensAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwarmJoinTokens("SWMTKN-worker", "SWMTKN-manager", "192.168.1.5"));

        var join = await Build(lifecycle).GetJoinInfoAsync();

        Assert.IsTrue(join.Available);
        Assert.AreEqual("192.168.1.5:2377", join.ManagerAddress);
        Assert.AreEqual("docker swarm join --token SWMTKN-worker 192.168.1.5:2377", join.WorkerJoinCommand);
    }
}
