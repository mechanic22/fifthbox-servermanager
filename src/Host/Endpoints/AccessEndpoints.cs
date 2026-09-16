using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FifthBox.ServerManager.Host.Endpoints;

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
