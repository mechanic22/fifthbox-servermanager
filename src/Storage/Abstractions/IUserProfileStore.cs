using FifthBox.ServerManager.Storage.Models;

namespace FifthBox.ServerManager.Storage.Abstractions;

/// <summary>
/// Stores user profiles. Thin adapter over the DB — no logic. <see cref="FindByEmailAsync"/> is what
/// the external-login resolver uses to attach a provider sign-in to an existing account.
/// </summary>
public interface IUserProfileStore
{
    Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserProfile?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Creates the profile, or replaces it if the user already has one.</summary>
    Task UpsertAsync(UserProfile profile, CancellationToken ct = default);
}
