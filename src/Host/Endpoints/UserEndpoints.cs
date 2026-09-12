using System.Security.Claims;
using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Users;
using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.Claims;
using FifthBox.Identity.Contracts;

namespace FifthBox.ServerManager.Host.Endpoints;

/// <summary>
/// User management — the joined view over the identity account and its app profile. Each endpoint
/// calls the identity service and the profile store and stitches them together with
/// <see cref="UserProfileComposition"/>; no logic here.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        // Admin provisioning. Creates the identity account, then its profile. The profile email set
        // here is what later lets the user attach a Google login (the external resolver matches on it).
        group.MapPost("/", async (CreateUserRequest request, IIdentityService identity, IUserProfileStore profiles, CancellationToken ct) =>
        {
            var account = await identity.AdminCreateUserAsync(
                new AdminCreateUserRequest { UserName = request.UserName, Password = request.Password, Roles = request.Roles }, ct);

            await UserProfileComposition.UpsertProfileAsync(profiles, account.UserId, request.Email, request.FirstName, request.LastName, ct);

            return TypedResults.Ok(await UserProfileComposition.ComposeAsync(account.UserId, identity, profiles, ct));
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        // The current user as one object: identity spine + app profile.
        group.MapGet("/me", async (ClaimsPrincipal user, IIdentityService identity, IUserProfileStore profiles, CancellationToken ct) =>
        {
            // Authenticated but no subject claim = a malformed/stale session cookie; 401, don't 500.
            var userId = user.FindFirstValue(IdentityClaimTypes.Subject)
                ?? throw new UnauthorizedException("Unauthorized.");
            return TypedResults.Ok(await UserProfileComposition.ComposeAsync(userId, identity, profiles, ct));
        }).RequireAuthorization();

        // Admin user management. The list, the role write and delete all carry rules (you can't change
        // your own roles or delete yourself, and the last administrator stays), so they live behind
        // IUserAdminService.
        group.MapGet("/", async (ClaimsPrincipal user, IUserAdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.ListAsync(user.ToCaller(), ct)))
            .RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapPut("/{id}/roles", async (string id, UpdateUserRolesRequest request, ClaimsPrincipal user, IUserAdminService svc, CancellationToken ct) =>
        {
            await svc.SetRolesAsync(user.ToCaller(), id, request.Roles, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, IUserAdminService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(user.ToCaller(), id, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AuthPolicies.AdminOnly);

        return app;
    }
}
