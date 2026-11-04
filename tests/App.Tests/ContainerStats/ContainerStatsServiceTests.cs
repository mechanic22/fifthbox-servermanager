using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests.ContainerStats;

[TestClass]
public class ContainerStatsServiceTests
{
    private readonly Mock<IWorkloadBackend> _backend = new();
    private readonly Mock<IPlatformServices> _platform = new();

    private ContainerStatsService Service(string? key = "signing-key")
    {
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(WorkloadKind.Container)).Returns(_backend.Object);
        return new ContainerStatsService(
            resolver.Object,
            _platform.Object,
            Options.Create(new ContainerStatsOptions { SigningKey = key }),
            Options.Create(new HostDeploymentOptions()),
            Options.Create(new ClusterOptions()));
    }

    private void Running(params (string Name, string Image)[] services) =>
        _platform.Setup(p => p.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services.Select(s => new PlatformServiceResponse { Name = s.Name, Image = s.Image, State = WorkloadState.Running }).ToList());

    [TestMethod]
    public void The_service_is_global_and_listens_on_nothing()
    {
        var d = Service().BuildDeployment("k", "1.0");

        Assert.AreEqual(WorkloadMode.Global, d.Mode);
        Assert.IsEmpty(d.Ports);
        Assert.AreEqual(PlatformLabels.PlatformRole, d.Labels![PlatformLabels.RoleKey]);
    }

    [TestMethod]
    public void The_service_is_hardened_and_limited()
    {
        var d = Service().BuildDeployment("k", "1.0");

        Assert.IsTrue(d.ReadOnlyRootFilesystem);
        Assert.IsTrue(d.DropAllCapabilities);
        Assert.IsGreaterThan(0, d.MemoryLimitMb!.Value);
        Assert.IsGreaterThan(0, d.CpuLimit!.Value);
    }

    [TestMethod]
    public void The_only_host_path_it_gets_is_the_docker_socket()
    {
        var mounts = Service().BuildDeployment("k", "1.0").Mounts;

        Assert.AreEqual("/var/run/docker.sock", mounts.Single(m => m.Type == VolumeMountType.Bind).Source);
    }

    [TestMethod]
    public void Disk_space_comes_from_an_empty_volume_mounted_read_only()
    {
        var probe = Service().BuildDeployment("k", "1.0").Mounts.Single(m => m.Type == VolumeMountType.Volume);

        Assert.IsTrue(probe.ReadOnly);
        Assert.AreEqual("disk-probe", probe.Source);
    }

    [TestMethod]
    public void Swarm_fills_in_the_node_id() =>
        Assert.AreEqual("{{.Node.ID}}", Service().BuildDeployment("k", "1.0").Env.Single(e => e.Key == "Stats__NodeId").Value);

    [TestMethod]
    public void The_key_goes_in_a_secret_and_never_in_env()
    {
        var d = Service().BuildDeployment("the-signing-key", "1.0");

        Assert.AreEqual("the-signing-key", d.Secrets.Single().Content);
        Assert.AreEqual("fbsm-stats-key", d.Secrets.Single().FileName);
        Assert.IsFalse(d.Env.Any(e => e.Value.Contains("the-signing-key")));
    }

    [TestMethod]
    public void It_reports_to_the_host_by_service_name() =>
        Assert.AreEqual("http://fbsm-host:8080", Service().BuildDeployment("k", "1.0").Env.Single(e => e.Key == "Stats__HostUrl").Value);

    [TestMethod]
    public async Task Deploy_uses_the_hosts_version()
    {
        Running(("fbsm-host", "docker.5thbox.com/fbsm/servermanager:0.4.3@sha256:abc"));

        await Service().DeployAsync();

        _backend.Verify(b => b.DeployAsync(It.Is<WorkloadDeployment>(d => d.Image == "docker.5thbox.com/fbsm/stats:0.4.3"), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Deploy_falls_back_to_latest_when_the_host_isnt_a_service()
    {
        Running();

        await Service().DeployAsync();

        _backend.Verify(b => b.DeployAsync(It.Is<WorkloadDeployment>(d => d.Image.EndsWith(":latest")), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Deploy_refuses_on_a_generated_encryption_key()
    {
        await Assert.ThrowsExactlyAsync<ConflictException>(() => Service(key: null).DeployAsync());

        _backend.Verify(b => b.DeployAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Status_says_when_it_cant_be_deployed()
    {
        Running();

        var status = await Service(key: null).GetAsync();

        Assert.IsFalse(status.Deployed);
        Assert.IsFalse(status.CanDeploy);
    }

    [TestMethod]
    public async Task Status_flags_stats_behind_the_host()
    {
        Running(("fbsm-host", "r/servermanager:0.4.3"), ("fbsm-stats", "r/stats:0.4.1@sha256:abc"));

        var status = await Service().GetAsync();

        Assert.AreEqual("0.4.1", status.Version);
        Assert.AreEqual("0.4.3", status.HostVersion);
        Assert.IsTrue(status.BehindHost);
    }

    [TestMethod]
    [DataRow("r/servermanager:0.4.3", "r/stats:0.4.3")]
    [DataRow("r/servermanager:latest", "r/stats:0.4.1")]
    public async Task Status_doesnt_flag_a_match_or_an_unversioned_host(string host, string stats)
    {
        Running(("fbsm-host", host), ("fbsm-stats", stats));

        Assert.IsFalse((await Service().GetAsync()).BehindHost);
    }

    [TestMethod]
    [DataRow("nginx", null)]
    [DataRow("nginx:1.27", "1.27")]
    [DataRow("nginx:1.27@sha256:abc", "1.27")]
    [DataRow("nginx@sha256:abc", null)]
    [DataRow("docker.5thbox.com:5000/fbsm/stats", null)]
    [DataRow("docker.5thbox.com:5000/fbsm/stats:2.0", "2.0")]
    [DataRow("", null)]
    public void Tag_is_read_from_the_image(string image, string? expected) =>
        Assert.AreEqual(expected, ContainerStatsService.TagOf(image));
}
