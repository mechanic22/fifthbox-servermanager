using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core.Tests;

[TestClass]
public class WorkloadPermissionsTests
{
    private static WorkloadResponse Workload(AccessLevel access, bool canDeploy = false)
        => new() { Id = "w1", Name = "site", Access = access, CanDeploy = canDeploy };

    [TestMethod]
    public void Levels_include_the_ones_below_them()
    {
        Assert.IsFalse(WorkloadPermissions.CanView(Workload(AccessLevel.None)));
        Assert.IsTrue(WorkloadPermissions.CanView(Workload(AccessLevel.View)));
        Assert.IsFalse(WorkloadPermissions.CanOperate(Workload(AccessLevel.View)));
        Assert.IsTrue(WorkloadPermissions.CanOperate(Workload(AccessLevel.Operate)));
        Assert.IsFalse(WorkloadPermissions.CanConfigure(Workload(AccessLevel.Operate)));
        Assert.IsTrue(WorkloadPermissions.CanConfigure(Workload(AccessLevel.Configure)));
        Assert.IsTrue(WorkloadPermissions.CanOperate(Workload(AccessLevel.Configure)));
    }

    [TestMethod]
    public void CanDeploy_takes_the_servers_answer_rather_than_guessing_from_the_level()
    {
        // DeployPermissionTests owns the truth table, this just checks the client doesn't second-guess it
        Assert.IsFalse(WorkloadPermissions.CanDeploy(Workload(AccessLevel.Configure, canDeploy: false)));
        Assert.IsTrue(WorkloadPermissions.CanDeploy(Workload(AccessLevel.Operate, canDeploy: true)));
    }
}
