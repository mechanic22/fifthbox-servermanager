using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Client.Web.Services;
using FifthBox.ServerManager.Shared.Users;
using Moq;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class CookieAuthenticationStateProviderTests
{
    private static CookieAuthenticationStateProvider CreateProvider(Mock<IAuthClient> auth) => new(auth.Object);

    [TestMethod]
    public async Task GetAuthenticationState_WhenNoCurrentUser_IsAnonymous()
    {
        var auth = new Mock<IAuthClient>();
        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserResponse?)null);

        var state = await CreateProvider(auth).GetAuthenticationStateAsync();

        Assert.IsFalse(state.User.Identity?.IsAuthenticated ?? false);
    }

    [TestMethod]
    public async Task GetAuthenticationState_WhenUser_BuildsAuthenticatedPrincipalWithClaims()
    {
        var auth = new Mock<IAuthClient>();
        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse
        {
            UserId = "u-1",
            UserName = "admin@demo.local",
            Email = "admin@demo.local",
            Roles = ["admin"]
        });

        var user = (await CreateProvider(auth).GetAuthenticationStateAsync()).User;

        Assert.IsTrue(user.Identity?.IsAuthenticated);
        Assert.AreEqual("admin@demo.local", user.Identity?.Name);
        Assert.AreEqual("u-1", user.FindFirst("sub")?.Value);
        Assert.IsTrue(user.IsInRole("admin"));
    }

    [TestMethod]
    public async Task GetAuthenticationState_WhenClientThrows_DegradesToAnonymous()
    {
        // a stale-session 404 comes back as null, anything else (backend down) mustn't crash auth state
        var auth = new Mock<IAuthClient>();
        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

        var state = await CreateProvider(auth).GetAuthenticationStateAsync();

        Assert.IsFalse(state.User.Identity?.IsAuthenticated ?? false);
    }

    [TestMethod]
    public async Task GetAuthenticationState_CachesUserResponse_AvoidingRedundantApiCalls()
    {
        var auth = new Mock<IAuthClient>();
        var user = new UserResponse { UserId = "u-1", UserName = "test", Email = "test@test.com", Roles = [] };
        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var provider = CreateProvider(auth);

        var state1 = await provider.GetAuthenticationStateAsync();
        Assert.IsTrue(state1.User.Identity?.IsAuthenticated);

        // still inside the 5 minute cache window
        var state2 = await provider.GetAuthenticationStateAsync();
        Assert.IsTrue(state2.User.Identity?.IsAuthenticated);

        auth.Verify(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NotifyStateChanged_InvalidatesCache_ForcingFreshFetch()
    {
        var auth = new Mock<IAuthClient>();
        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse { UserId = "u-1", UserName = "original", Email = "test@test.com", Roles = [] });

        var provider = CreateProvider(auth);
        await provider.GetAuthenticationStateAsync();

        auth.Setup(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse { UserId = "u-2", UserName = "updated", Email = "new@test.com", Roles = [] });
        provider.NotifyStateChanged();

        var state = await provider.GetAuthenticationStateAsync();
        Assert.AreEqual("updated", state.User.Identity?.Name);

        auth.Verify(a => a.GetCurrentUserAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
