using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

/// Cluster status is visible to any signed-in user; the sensitive/mutating bits (join tokens,
/// bootstrap) are admin-only. Translate + delegate only.
public static class ClusterEndpoints
{
    public static IEndpointRouteBuilder MapClusterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cluster").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (IClusterService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetStatusAsync(ct)));

        group.MapGet("/join", async (IClusterService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetJoinInfoAsync(ct)));

        // The explicit opt-in bootstrap — never runs at startup, only when an admin asks.
        group.MapPost("/bootstrap", async (IClusterService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.BootstrapAsync(ct)));

        return app;
    }
}
