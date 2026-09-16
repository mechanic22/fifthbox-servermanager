using System.Security.Claims;
using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Teams;

namespace FifthBox.ServerManager.Host.Endpoints;

public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/teams").RequireAuthorization(AuthPolicies.AdminOnly);

        api.MapGet("/", async (ClaimsPrincipal user, ITeamService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(user.ToCaller(), ct)));

        api.MapPost("/", async (SaveTeamRequest request, ClaimsPrincipal user, ITeamService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CreateAsync(user.ToCaller(), request, ct)));

        api.MapPut("/{id}", async (string id, SaveTeamRequest request, ClaimsPrincipal user, ITeamService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpdateAsync(user.ToCaller(), id, request, ct)));

        api.MapPut("/{id}/members", async (string id, SetTeamMembersRequest request, ClaimsPrincipal user, ITeamService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.SetMembersAsync(user.ToCaller(), id, request, ct)));

        api.MapDelete("/{id}", async (string id, ClaimsPrincipal user, ITeamService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
