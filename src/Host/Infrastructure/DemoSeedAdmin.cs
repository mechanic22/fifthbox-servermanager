using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.Contracts;
using FifthBox.Identity.Exceptions;

namespace FifthBox.ServerManager.Host.Infrastructure;

/// first admin, since creating admins needs an admin. blank config skips it, safe every startup
internal static class DemoSeedAdmin
{
    public static async Task SeedDemoSeedAdminAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection("Identity:DemoSeedAdmin");
        var userName = section["UserName"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var profiles = scope.ServiceProvider.GetRequiredService<IUserProfileStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoSeedAdmin));

        try
        {
            var account = await identity.AdminCreateUserAsync(
                new AdminCreateUserRequest { UserName = userName, Password = password, Roles = [Roles.Admin] });
            await UserProfileComposition.UpsertProfileAsync(profiles, account.UserId, userName, string.Empty, string.Empty, CancellationToken.None);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seeded demo seed admin {UserName}", userName);
            }
        }
        catch (DuplicateUserNameException)
        {
            // already seeded
        }
    }
}
