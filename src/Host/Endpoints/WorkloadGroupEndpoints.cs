using System.Security.Claims;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Endpoints;

/// deleting a group ungroups its workloads, doesn't delete them
public static class WorkloadGroupEndpoints
{
    public static IEndpointRouteBuilder MapWorkloadGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/workload-groups").RequireAuthorization();

        api.MapGet("/", async (ClaimsPrincipal user, IWorkloadGroupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(user.ToCaller(), ct)));

        api.MapPost("/", async (CreateWorkloadGroupRequest request, ClaimsPrincipal user, IWorkloadGroupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(user.ToCaller(), request, ct)))
            .RequireAuthorization(AuthPolicies.AdminOnly);

        api.MapPut("/{id}", async (string id, UpdateWorkloadGroupRequest request, ClaimsPrincipal user, IWorkloadGroupService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RenameAsync(user.ToCaller(), id, request, ct)))
            .RequireAuthorization(AuthPolicies.AdminOnly);

        api.MapDelete("/{id}", async (string id, ClaimsPrincipal user, IWorkloadGroupService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        return app;
    }
}
