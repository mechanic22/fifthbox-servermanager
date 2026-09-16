using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Host.Realtime;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

/// enroll is anonymous, the key in the body is the gate
/// agents make no docker events, so enroll and delete refresh the machine list themselves
public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agents");

        group.MapPost("/enroll", async (EnrollAgentRequest request, IAgentService svc, IClusterStateWriter cluster, CancellationToken ct) =>
        {
            var enrolled = await svc.EnrollAsync(request, ct);
            await cluster.RefreshNodesAsync(ct);
            return TypedResults.Ok(enrolled);
        });

        group.MapGet("/", async (IAgentService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(ct))).RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapDelete("/{id}", async (string id, IAgentService svc, IClusterStateWriter cluster, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            await cluster.RefreshNodesAsync(ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapPost("/enrollment-key", async (IAgentService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GenerateEnrollmentKeyAsync(ct))).RequireAuthorization(AuthPolicies.AdminOnly);

        return app;
    }
}
