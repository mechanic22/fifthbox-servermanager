using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class DeployPermissionTests
{
    [TestMethod]
    public void Configure_can_deploy()
    {
        Assert.IsTrue(DeployPermission.Allowed(AccessLevel.Configure));
    }

    [TestMethod]
    public void Operate_cannot_deploy_it_publishes_config()
    {
        // bringing a stopped workload back is Start, which redeploys the running revision
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.Operate));
    }

    [TestMethod]
    public void View_and_none_cannot_deploy()
    {
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.View));
        Assert.IsFalse(DeployPermission.Allowed(AccessLevel.None));
    }
}
