using FifthBox.ServerManager.Shared.Users;
using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.ServerManager.Storage.Models;
using FifthBox.Identity.Abstractions;

namespace FifthBox.ServerManager.Host.Infrastructure;

internal static class UserProfileComposition
{
    /// store keeps the original CreatedAt on update
    public static Task UpsertProfileAsync(
        IUserProfileStore profiles, string userId, string email, string firstName, string lastName, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return profiles.UpsertAsync(new UserProfile
        {
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
    }

    public static async Task<UserResponse> ComposeAsync(
        string userId, IIdentityService identity, IUserProfileStore profiles, CancellationToken ct)
    {
        var account = await identity.GetByIdAsync(userId, ct);
        var profile = await profiles.FindByUserIdAsync(userId, ct);

        return new UserResponse
        {
            UserId = account.UserId,
            UserName = account.UserName,
            Roles = account.Roles,
            Email = profile?.Email ?? string.Empty,
            FirstName = profile?.FirstName ?? string.Empty,
            LastName = profile?.LastName ?? string.Empty,
            LinkedProviders = [.. account.ExternalLogins.Select(e => e.Provider)]
        };
    }
}
