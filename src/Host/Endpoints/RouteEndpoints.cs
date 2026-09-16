using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class RouteEndpoints
{
    public static IEndpointRouteBuilder MapRouteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/routes").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct)));

        // literal route wins over /{id}
        group.MapGet("/nginx-config", async (IRouteService svc, CancellationToken ct) =>
            TypedResults.Text(await svc.RenderConfigAsync(ct), "text/plain"));

        group.MapGet("/proxy-status", async (IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetProxyStatusAsync(ct)));

        group.MapGet("/proxy-state", async (IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetProxyStateAsync(ct)));

        group.MapPost("/apply", async (IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ApplyAsync(ct)));

        // own verb so an edit can never flip it by accident
        group.MapPatch("/{id}/enabled", async (string id, SetRouteEnabledRequest request, IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.SetEnabledAsync(id, request.Enabled, ct)));

        group.MapGet("/{id}", async (string id, IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAsync(id, ct)));

        group.MapPost("/", async (CreateRouteRequest request, IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(request, ct)));

        group.MapPut("/{id}", async (string id, UpdateRouteRequest request, IRouteService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(id, request, ct)));

        group.MapDelete("/{id}", async (string id, IRouteService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
