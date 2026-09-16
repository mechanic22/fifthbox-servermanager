using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class RolloutTests
{
    private static WorkloadRuntimeStatus Status(string? updateState, bool deployed = true) =>
        new() { Name = "web", Deployed = deployed, UpdateState = updateState };

    [TestMethod]
    public void A_freshly_created_service_counts_as_applied()
    {
        // swarm writes no UpdateStatus until something updates the service, so a first deploy would never settle
        Assert.AreEqual(RolloutOutcome.Applied, Rollout.Of(Status(null)));
    }

    [TestMethod]
    public void A_completed_rollout_is_applied() =>
        Assert.AreEqual(RolloutOutcome.Applied, Rollout.Of(Status("completed")));

    [TestMethod]
    public void A_finished_rollback_is_reverted() =>
        Assert.AreEqual(RolloutOutcome.Reverted, Rollout.Of(Status("rollback_completed")));

    [TestMethod]
    [DataRow("updating")]
    [DataRow("rollback_started")]
    [DataRow("paused")]
    public void An_unfinished_rollout_settles_nothing(string updateState) =>
        Assert.AreEqual(RolloutOutcome.Unsettled, Rollout.Of(Status(updateState)));

    [TestMethod]
    public void Nothing_deployed_settles_nothing() =>
        Assert.AreEqual(RolloutOutcome.Unsettled, Rollout.Of(Status("completed", deployed: false)));
}
