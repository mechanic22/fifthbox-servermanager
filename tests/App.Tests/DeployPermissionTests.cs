using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class DeployPermissionTests
{
    [TestMethod]
    public void Configure_can_always_deploy()
    {
        Assert.IsTrue(DeployPermission.Allowed(AccessLevel.Configure, hasRevision: false, hasPendingChanges: false));
        Assert.IsTrue(DeployPermission.Allowed(AccessLevel.Configure, hasRevision: true, hasPendingChanges: true));
    }

    [TestMethod]
    public void Operate_can_redeploy_a_running_workload_with_nothing_unpublished()
    {
        Assert.IsTrue(DeployPermission.Allowed(AccessLevel.Operate, hasRevision: true, hasPendingChanges: false));
    }

    [TestMethod]
    public void Operate_cannot_publish_pending_changes()
    {
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.Operate, hasRevision: true, hasPendingChanges: true));
    }

    [TestMethod]
    public void Operate_cannot_perform_the_first_deploy()
    {
        // HasPendingChanges is false before the first deploy, which is why hasRevision exists
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.Operate, hasRevision: false, hasPendingChanges: false));
    }

    [TestMethod]
    public void View_and_none_cannot_deploy()
    {
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.View, hasRevision: true, hasPendingChanges: false));
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.None, hasRevision: true, hasPendingChanges: false));
    }
}
