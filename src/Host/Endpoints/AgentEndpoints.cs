using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Host.Realtime;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Host.Endpoints;

/// Agent management. Enrollment is anonymous (gated by the enrollment key in the body); listing,
/// deleting, and minting keys are admin-only.
///
/// Enrolling and deleting change the machine list, and nothing else observes that — an agent produces no
/// docker event — so these tell the writer directly rather than leaving it to the reconcile a minute later.
/// The refresh is a no-op for clients when nothing actually changed.
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
