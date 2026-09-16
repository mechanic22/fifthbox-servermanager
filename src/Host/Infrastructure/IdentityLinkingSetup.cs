using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.Identity;

namespace FifthBox.ServerManager.Host.Infrastructure;

public static class IdentityLinkingSetup
{
    /// first external login links to the account with that profile email. no match under AdminOnly is a 403
    /// post-build because it needs scoped stores. IdentityOptions is a singleton so mutating it is fine
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
