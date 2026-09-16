using System.Security.Claims;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Users;
using FifthBox.Identity.Blazor;
using FifthBox.Identity.Claims;

namespace FifthBox.ServerManager.Client.Web.Services;

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
