using System.Security.Claims;
using FifthBox.Identity.Claims;
using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.Host.Infrastructure;

public static class CallerExtensions
{
    public static Caller ToCaller(this ClaimsPrincipal user)
    {
        // no sub claim means a stale session, 401 not 500
        var userId = user.FindFirstValue(IdentityClaimTypes.Subject)
            ?? throw new UnauthorizedException("Unauthorized.");

        return new Caller(userId, user.IsInRole(Roles.Admin));
    }
}
