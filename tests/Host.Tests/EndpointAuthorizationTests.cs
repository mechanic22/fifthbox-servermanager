using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Host.Tests;

[TestClass]
public class EndpointAuthorizationTests
{
    private static AppFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _factory = new AppFactory();

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    private static readonly Dictionary<string, string> AnonymousByDesign = new(StringComparer.Ordinal)
    {
        ["/healthz"] = "liveness probe",
        ["/.well-known/acme-challenge/{token}"] = "Let's Encrypt fetches this before any cert exists",
        ["/{*path:nonfile}"] = "the WASM app shell — static files, no data, and the login page lives in it",
        ["/{**path:file}"] = "MapStaticAssets' own fallback, so an unknown file name 404s instead of being answered with the app shell",

        ["/api/auth/login"] = "signing in is how you stop being anonymous",
        ["/api/auth/logout"] = "clearing a cookie must work even with a stale session",
        ["/api/auth/registration"] = "tells the login page whether to offer registration",
        ["/api/auth/register"] = "self-service registration; the policy is enforced inside RegisterAsync",
        ["/api/auth/external/{provider}/challenge"] = "OAuth start",
        ["/api/auth/external/{provider}/callback"] = "OAuth return leg",
        ["/api/auth/native/login"] = "signing in from a native client",
        ["/api/auth/native/register"] = "same registration policy, bearer flavour",
        ["/api/auth/native/refresh"] = "the refresh token is the credential",
        ["/api/auth/native/revoke"] = "the refresh token is the credential",

        ["/api/agents/enroll"] = "the enrollment key is the credential; the agent has no account yet",
        // AgentHub has no [Authorize], agents aren't Identity users and it checks agentId:secret in OnConnectedAsync
        ["/hubs/agents"] = "agents authenticate with their own credential inside the hub",
        ["/hubs/agents/negotiate"] = "agents authenticate with their own credential inside the hub",
    };

    [TestMethod]
    public void Every_endpoint_requires_authorization_unless_it_is_on_the_open_list()
    {
        var unprotected = Routes()
            .Where(r => !AnonymousByDesign.ContainsKey(r.Pattern))
            .Where(r => !r.Endpoint.Metadata.OfType<IAuthorizeData>().Any()
                     || r.Endpoint.Metadata.OfType<IAllowAnonymous>().Any())
            .Select(r => r.Pattern)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.IsEmpty(
            unprotected,
            "These routes are reachable without signing in. Add RequireAuthorization, or add them to " +
            $"AnonymousByDesign with a reason: {string.Join(", ", unprotected)}");
    }

    [TestMethod]
    public void The_open_list_has_no_entries_for_routes_that_no_longer_exist()
    {
        // otherwise a renamed route stays excused by its old name
        var patterns = Routes().Select(r => r.Pattern).ToHashSet(StringComparer.Ordinal);
        var stale = AnonymousByDesign.Keys.Where(p => !patterns.Contains(p)).ToList();

        Assert.IsEmpty(stale, $"Open list mentions routes that are gone: {string.Join(", ", stale)}");
    }

    [TestMethod]
    public void The_platform_surface_is_administrator_only()
    {
        // only workloads, workload groups and /api/users are open to granted non-admins
        string[] adminOnly = ["/api/nodes", "/api/cluster", "/api/routes", "/api/tls", "/api/registries",
                              "/api/system", "/api/teams", "/api/access"];

        var leaked = Routes()
            .Where(r => adminOnly.Any(prefix => r.Pattern.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(r => !r.Endpoint.Metadata.OfType<IAuthorizeData>().Any(a => a.Policy == AuthPolicies.AdminOnly))
            .Select(r => r.Pattern)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.IsEmpty(leaked, $"Not AdminOnly: {string.Join(", ", leaked)}");
    }

    private static List<(string Pattern, RouteEndpoint Endpoint)> Routes()
    {
        var source = _factory.Services.GetRequiredService<EndpointDataSource>();
        return
        [
            .. source.Endpoints
                .OfType<RouteEndpoint>()
                // static assets are the wasm app a signed-out browser needs to reach login,
                // listing a thousand files here would bury the real decisions
                .Where(e => !e.Metadata.OfType<StaticAssetDescriptor>().Any())
                .Select(e => ("/" + e.RoutePattern.RawText!.TrimStart('/'), e))
        ];
    }
}
