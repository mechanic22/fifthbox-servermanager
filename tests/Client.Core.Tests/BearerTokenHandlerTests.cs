using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Auth;
using Moq;

namespace FifthBox.ServerManager.Client.Core.Tests;

[TestClass]
public class BearerTokenHandlerTests
{
    [TestMethod]
    public async Task AttachesAccessToken_AndPassesThrough_On200()
    {
        var store = new InMemoryTokenStore(access: "abc");
        var session = new Mock<IAuthSession>();
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var response = await Send(store, session.Object, stub);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("abc", stub.Seen.Single().Auth);
        session.Verify(s => s.SignOutAsync(), Times.Never);
    }

    [TestMethod]
    public async Task On401_RefreshSucceeds_ReplaysRequestWithNewToken()
    {
        var store = new InMemoryTokenStore(access: "old", refresh: "r1");
        var session = new Mock<IAuthSession>();

        // The resource is 401 while the token is stale, 200 once the refresh has swapped in the new one.
        var stub = new StubHandler(req => req.RequestUri!.AbsoluteUri.Contains("native/refresh")
            ? Json(new NativeAuthResponse { AccessToken = "new", RefreshToken = "r2", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) })
            : new HttpResponseMessage(store.Access == "new" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized));

        var response = await Send(store, session.Object, stub);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("new", store.Access);
        Assert.AreEqual("r2", store.Refresh);
        Assert.AreEqual(1, store.SaveCount);

        // resource(401) → refresh → resource(200), and the replay carried the refreshed token.
        Assert.HasCount(3, stub.Seen);
        Assert.AreEqual("new", stub.Seen[2].Auth);
        session.Verify(s => s.SignOutAsync(), Times.Never);
    }

    [TestMethod]
    public async Task On401_RefreshRejected_ClearsTokensAndSignsOut()
    {
        var store = new InMemoryTokenStore(access: "old", refresh: "r1");
        var session = new Mock<IAuthSession>();
        // Every call 401 — including the refresh (the reuse/expiry signal).
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var response = await Send(store, session.Object, stub);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsNull(store.Access);
        Assert.AreEqual(1, store.ClearCount);
        session.Verify(s => s.SignOutAsync(), Times.Once);
    }

    [TestMethod]
    public async Task On401_NoRefreshToken_SignsOutWithoutAttemptingRefresh()
    {
        var store = new InMemoryTokenStore(access: "old", refresh: null);
        var session = new Mock<IAuthSession>();
        var stub = new StubHandler(req =>
        {
            Assert.DoesNotContain("native/refresh", req.RequestUri!.AbsoluteUri, "must not attempt refresh without a refresh token");
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var response = await Send(store, session.Object, stub);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.HasCount(1, stub.Seen);
        session.Verify(s => s.SignOutAsync(), Times.Once);
    }

    private static async Task<HttpResponseMessage> Send(ITokenStore store, IAuthSession session, HttpMessageHandler inner)
    {
        var invoker = new HttpMessageInvoker(new BearerTokenHandler(store, session) { InnerHandler = inner });
        return await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost:5080/api/nodes"), CancellationToken.None);
    }

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
