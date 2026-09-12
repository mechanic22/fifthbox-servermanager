using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;
using Moq;

namespace FifthBox.ServerManager.Client.Core.Tests;

[TestClass]
public class NativeAuthClientTests
{
    [TestMethod]
    public async Task LoginAsync_StoresTokenPair_AndSetsSessionUser()
    {
        var store = new InMemoryTokenStore();
        var session = new Mock<IAuthSession>();
        var stub = new StubHandler(req =>
        {
            var uri = req.RequestUri!.AbsoluteUri;
            if (uri.Contains("native/login"))
            {
                return Json(new NativeAuthResponse { UserId = "u1", AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
            }
            if (uri.Contains("users/me"))
            {
                return Json(new UserResponse { UserId = "u1", UserName = "user@demo.local", Email = "user@demo.local" });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new NativeAuthClient(Http(stub), store, session.Object);

        await client.LoginAsync(new LoginRequest { UserName = "user@demo.local", Password = "pw" });

        Assert.AreEqual("a", store.Access);
        Assert.AreEqual("r", store.Refresh);
        session.Verify(s => s.SetUser(It.Is<UserResponse>(u => u.UserId == "u1")), Times.Once);
    }

    [TestMethod]
    public async Task LogoutAsync_ClearsStore_AndSignsOut()
    {
        var store = new InMemoryTokenStore(access: "a", refresh: "r");
        var session = new Mock<IAuthSession>();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var client = new NativeAuthClient(Http(stub), store, session.Object);

        await client.LogoutAsync();

        Assert.IsNull(store.Access);
        Assert.AreEqual(1, store.ClearCount);
        session.Verify(s => s.SignOutAsync(), Times.Once);
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://localhost:5080/") };

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
