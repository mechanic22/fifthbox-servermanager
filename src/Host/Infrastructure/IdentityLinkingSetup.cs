using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.Identity;

namespace FifthBox.ServerManager.Host.Infrastructure;

public static class IdentityLinkingSetup
{
    /// <summary>
    /// Hooks Identity's external-login resolver up to the app's profile store: a first-time external
    /// sign-in attaches to an existing account whose profile email matches the provider email. Identity
    /// never matches on email itself (unreliable across providers); this app-owned hook does, against
    /// Storage.
    /// <para>
    /// With an <c>AdminOnly</c> policy this is the gate we wanted: an unknown external user has no
    /// matching profile, the resolver returns null, and Identity refuses the sign-in (403) instead of
    /// self-provisioning.
    /// </para>
    /// Done post-build because the resolver grabs a scoped store per call — it needs the built
    /// container. <see cref="IdentityOptions"/> is a singleton, so mutating the delegate here is fine.
    /// </summary>
    public static WebApplication UseExternalLoginProfileResolver(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IdentityOptions>();
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

        options.ResolveExternalUserAsync = async (info, ct) =>
        {
            if (string.IsNullOrWhiteSpace(info.Email))
            {
                return null;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var profiles = scope.ServiceProvider.GetRequiredService<IUserProfileStore>();
            var profile = await profiles.FindByEmailAsync(info.Email, ct);
            return profile?.UserId;
        };

        return app;
    }
}
