using System.Security.Claims;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Users;
using FifthBox.Identity.Blazor;
using FifthBox.Identity.Claims;

namespace FifthBox.ServerManager.Client.Web.Services;

/// <summary>
/// App auth-state provider. The caching, single-flight, never-throw plumbing and the claim name/role
/// contract now live in <see cref="CookieAuthenticationStateProvider{TUser}"/> (FifthBox.Identity.Blazor).
/// All that's left app-side: how to fetch the current user and how to map the app's
/// <see cref="UserResponse"/> to claims — using <see cref="IdentityClaimTypes"/> so nothing has to
/// reverse-engineer the Host's claim names.
/// </summary>
public sealed class CookieAuthenticationStateProvider(IAuthClient auth)
    : CookieAuthenticationStateProvider<UserResponse>
{
    protected override Task<UserResponse?> FetchCurrentUserAsync() => auth.GetCurrentUserAsync();

    protected override IEnumerable<Claim> BuildClaims(UserResponse user)
    {
        yield return new Claim(IdentityClaimTypes.Subject, user.UserId);
        yield return new Claim(IdentityClaimTypes.Name, user.UserName);
        yield return new Claim(IdentityClaimTypes.Email, user.Email);
        foreach (var role in user.Roles)
        {
            yield return new Claim(IdentityClaimTypes.Role, role);
        }
    }
}
