using System.Security.Claims;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Endpoints;

/// access is checked per workload, create and delete also need AdminOnly at the route
public static class WorkloadEndpoints
{
    public static IEndpointRouteBuilder MapWorkloadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workloads").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(user.ToCaller(), ct)));

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAsync(user.ToCaller(), id, ct)));

        group.MapPost("/", async (CreateWorkloadRequest request, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(user.ToCaller(), request, ct)))
            .RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapPut("/{id}", async (string id, UpdateWorkloadRequest request, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(user.ToCaller(), id, request, ct)));

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        // literal route wins over /{id}
        group.MapGet("/statuses", async (ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetStatusesAsync(user.ToCaller(), ct)));

        group.MapGet("/{id}/status", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetStatusAsync(user.ToCaller(), id, ct)));

        group.MapGet("/{id}/logs", async (string id, int? tail, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetLogsAsync(user.ToCaller(), id, tail ?? 200, ct)));

        group.MapGet("/{id}/routes", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetRoutesAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/deploy", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.DeployAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/scale", async (string id, ScaleWorkloadRequest request, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ScaleAsync(user.ToCaller(), id, request.Replicas, ct)));

        group.MapGet("/{id}/move-targets", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.MoveTargetsAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/move", async (string id, MoveWorkloadRequest request, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.MoveAsync(user.ToCaller(), id, request.AgentId, ct)));

        group.MapPost("/{id}/restart", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RestartAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/update", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(user.ToCaller(), id, ct)));

        group.MapGet("/{id}/files", async (string id, string? path, ClaimsPrincipal user, IWorkloadFileService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(user.ToCaller(), id, path, ct)));

        group.MapGet("/{id}/files/content", async (string id, string path, ClaimsPrincipal user, IWorkloadFileService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ReadAsync(user.ToCaller(), id, path, ct)));

        group.MapPut("/{id}/files/content", async (string id, WriteWorkloadFileRequest request, ClaimsPrincipal user, IWorkloadFileService svc, CancellationToken ct) =>
        {
            await svc.WriteAsync(user.ToCaller(), id, request.Path, request.Text, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{id}/console", async (string id, SendConsoleRequest request, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
        {
            await svc.SendConsoleAsync(user.ToCaller(), id, request.Text, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{id}/start", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.StartAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/stop", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
        {
            await svc.StopAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        });

        group.MapGet("/{id}/drift", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetDriftAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/undeploy", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
        {
            await svc.UndeployAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        });

        group.MapGet("/{id}/revisions", async (string id, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetRevisionsAsync(user.ToCaller(), id, ct)));

        group.MapPost("/{id}/revert/{number:int}", async (string id, int number, ClaimsPrincipal user, IWorkloadService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.RevertAsync(user.ToCaller(), id, number, ct)));

        return app;
    }
}
