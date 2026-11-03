using FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class WorkloadPrimaryActionTests
{
    private static WorkloadAction For(
        bool canDeploy = true,
        bool canOperate = true,
        bool deploying = false,
        bool neverDeployed = false,
        bool stalled = false,
        bool drift = false,
        bool pendingChanges = false,
        bool gone = false,
        bool stopped = false) =>
        WorkloadPrimaryAction.For(canDeploy, canOperate, deploying, neverDeployed, stalled, drift, pendingChanges, gone, stopped);

    [TestMethod]
    public void A_healthy_running_workload_has_nothing_to_do() =>
        Assert.AreEqual(WorkloadAction.None, For());

    [TestMethod]
    [DataRow(true, false, false, false, false)]
    [DataRow(false, true, false, false, false)]
    [DataRow(false, false, true, false, false)]
    [DataRow(false, false, false, true, false)]
    [DataRow(false, false, false, false, true)]
    public void Anything_out_of_step_with_config_asks_for_a_deploy(bool neverDeployed, bool stalled, bool drift, bool pending, bool gone) =>
        Assert.AreEqual(WorkloadAction.Deploy, For(neverDeployed: neverDeployed, stalled: stalled, drift: drift, pendingChanges: pending, gone: gone));

    [TestMethod]
    public void Nothing_to_do_while_a_deploy_is_running() =>
        Assert.AreEqual(WorkloadAction.None, For(deploying: true, pendingChanges: true));

    [TestMethod]
    public void A_stopped_workload_offers_start() =>
        Assert.AreEqual(WorkloadAction.Start, For(stopped: true));

    [TestMethod]
    public void Pending_changes_on_a_stopped_workload_ask_for_a_deploy() =>
        Assert.AreEqual(WorkloadAction.Deploy, For(stopped: true, pendingChanges: true));

    [TestMethod]
    public void Never_deployed_can_only_deploy_so_someone_who_cant_gets_nothing() =>
        Assert.AreEqual(WorkloadAction.None, For(canDeploy: false, neverDeployed: true, stopped: true));

    [TestMethod]
    public void Without_deploy_rights_a_stopped_workload_still_offers_start() =>
        Assert.AreEqual(WorkloadAction.Start, For(canDeploy: false, pendingChanges: true, stopped: true));

    [TestMethod]
    public void Without_operate_rights_a_stopped_workload_offers_nothing() =>
        Assert.AreEqual(WorkloadAction.None, For(canOperate: false, stopped: true));
}
