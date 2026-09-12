using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Host.Endpoints;

/// The cluster's nodes, and the swarm operations on them. Admin-only: hostnames, addresses and swarm
/// health are the operator's business, not something a user granted one workload has any reason to read.
public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/nodes").RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapGet("/", async (INodeService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct)));

        group.MapGet("/{id}", async (string id, INodeService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAsync(id, ct)));   // throws NotFoundException → middleware → 404

        group.MapPut("/{id}/availability", async (string id, SetNodeAvailabilityRequest request, INodeService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.SetAvailabilityAsync(id, request.Availability, ct)));

        group.MapPut("/{id}/role", async (string id, SetNodeRoleRequest request, INodeService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.SetRoleAsync(id, request.Role, ct)));

        group.MapDelete("/{id}", async (string id, INodeService svc, CancellationToken ct) =>
        {
            await svc.RemoveAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
