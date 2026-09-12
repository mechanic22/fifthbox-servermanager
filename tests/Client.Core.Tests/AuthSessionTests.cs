using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core.Tests;

[TestClass]
public class AuthSessionTests
{
    [TestMethod]
    public void SetUser_UpdatesState_AndRaisesStateChanged()
    {
        var session = new AuthSession();
        var raised = 0;
        session.StateChanged += () => raised++;

        Assert.IsFalse(session.IsSignedIn);

        session.SetUser(new UserResponse { UserId = "u1", UserName = "user@demo.local" });

        Assert.IsTrue(session.IsSignedIn);
        Assert.AreEqual("u1", session.CurrentUser!.UserId);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public async Task SignOutAsync_ClearsUser_AndRaisesStateChanged()
    {
        var session = new AuthSession();
        session.SetUser(new UserResponse { UserId = "u1", UserName = "user@demo.local" });
        var raised = 0;
        session.StateChanged += () => raised++;

        await session.SignOutAsync();

        Assert.IsFalse(session.IsSignedIn);
        Assert.IsNull(session.CurrentUser);
        Assert.AreEqual(1, raised);
    }
}
