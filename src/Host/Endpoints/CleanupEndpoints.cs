using FifthBox.ServerManager.App.Cleanup;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class CleanupEndpoints
{
    public static IEndpointRouteBuilder MapCleanupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cleanup/images").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (IImageCleanupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.PreviewAsync(ct)));

        group.MapPost("/remove", async (IImageCleanupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RemoveUnusedAsync(ct)));

        return app;
    }
}
