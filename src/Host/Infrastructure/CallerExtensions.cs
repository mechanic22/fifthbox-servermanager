using System.Security.Claims;
using FifthBox.Identity.Claims;
using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.Host.Infrastructure;

public static class CallerExtensions
{
    /// Turns the request's principal into the plain value App works with.
    public static Caller ToCaller(this ClaimsPrincipal user)
    {
        // Authenticated but no subject claim = a malformed/stale session; 401, don't 500.
        var userId = user.FindFirstValue(IdentityClaimTypes.Subject)
            ?? throw new UnauthorizedException("Unauthorized.");

        return new Caller(userId, user.IsInRole(Roles.Admin));
    }
}
