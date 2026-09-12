using FifthBox.ServerManager.Shared.Users;
using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.ServerManager.Storage.Models;
using FifthBox.Identity.Abstractions;

namespace FifthBox.ServerManager.Host.Infrastructure;

/// <summary>
/// Glue between the two halves of "a user": the identity account and the Storage-owned profile (app
/// fields). The Host is where they meet — neither component reaches into the other. Endpoints stay
/// thin by pushing the join here.
/// </summary>
internal static class UserProfileComposition
{
    /// <summary>Creates or updates the app profile for an account. Timestamps set here; the store keeps
    /// the original <c>CreatedAt</c> on update.</summary>
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

    /// <summary>Reads the identity account and app profile and builds the client-facing view.</summary>
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
