using FifthBox.ServerManager.App.Cleanup;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadVolumeCleanupTests
{
    private readonly Mock<IDockerVolumes> _volumes = new();

    private WorkloadVolumeCleanup Cleanup() => new(_volumes.Object) { RetryDelay = TimeSpan.Zero, Attempts = 3 };

    private static WorkloadDeployment Deployment(params VolumeMount[] mounts) => new() { Name = "fbsm-stjohn", Mounts = mounts };

    [TestMethod]
    public void Only_named_volumes_of_container_workloads_count()
    {
        var deployment = Deployment(
            new VolumeMount(VolumeMountType.Volume, "data", "/data", false),
            new VolumeMount(VolumeMountType.Bind, "/srv/x", "/x", false));

        CollectionAssert.AreEqual(new[] { "fbsm-stjohn--data" }, WorkloadVolumeCleanup.VolumeNames(deployment, WorkloadKind.Container).ToArray());
        Assert.IsEmpty(WorkloadVolumeCleanup.VolumeNames(deployment, WorkloadKind.Native));
    }

    [TestMethod]
    public async Task Waits_for_a_volume_the_stopping_container_still_holds()
    {
        _volumes.SetupSequence(v => v.RemoveAsync("v", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VolumeRemoval.InUse)
            .ReturnsAsync(VolumeRemoval.Removed);

        Assert.IsEmpty(await Cleanup().RemoveAsync(["v"]));
    }

    [TestMethod]
    public async Task Gives_up_after_the_attempts_and_reports_what_stayed()
    {
        _volumes.Setup(v => v.RemoveAsync("busy", It.IsAny<CancellationToken>())).ReturnsAsync(VolumeRemoval.InUse);
        _volumes.Setup(v => v.RemoveAsync("gone", It.IsAny<CancellationToken>())).ReturnsAsync(VolumeRemoval.Missing);
        _volumes.Setup(v => v.RemoveAsync("ok", It.IsAny<CancellationToken>())).ReturnsAsync(VolumeRemoval.Removed);

        var kept = await Cleanup().RemoveAsync(["busy", "gone", "ok"]);

        CollectionAssert.AreEqual(new[] { "busy", "gone" }, kept.ToArray());
        _volumes.Verify(v => v.RemoveAsync("busy", It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
