using FifthBox.ServerManager.Storage.Models;

namespace FifthBox.ServerManager.Storage.Abstractions;

public interface IUserProfileStore
{
    Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserProfile?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// replaces it if the user already has one
    Task UpsertAsync(UserProfile profile, CancellationToken ct = default);
}
