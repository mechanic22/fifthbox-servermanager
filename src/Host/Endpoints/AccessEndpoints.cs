using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FifthBox.ServerManager.Host.Endpoints;

/// Access grants — who may do what to which group or workload. Managing them is the administrator's
/// job, so the whole group is admin-only; the levels granted here are enforced in the App layer on
/// every workload call.
public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/access").RequireAuthorization(AuthPolicies.AdminOnly);

        api.MapGet("/grants", async (IAccessService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct)));

        api.MapGet("/grants/{scope}/{targetId}", async (
            AccessScope scope, string targetId, IAccessService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListForTargetAsync(scope, targetId, ct)));

        api.MapPut("/grants", async Task<Results<Ok<AccessGrantResponse>, NoContent>> (
            SetAccessGrantRequest request, IAccessService svc, CancellationToken ct) =>
        {
            var grant = await svc.SetAsync(request, ct);
            return grant is null ? TypedResults.NoContent() : TypedResults.Ok(grant);
        });

        api.MapDelete("/grants/{id}", async (string id, IAccessService svc, CancellationToken ct) =>
        {
            await svc.RemoveAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
