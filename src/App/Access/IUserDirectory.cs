using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Access;

/// A user as the access layer needs to see one. The Identity package has no way to enumerate accounts,
/// so Storage — which owns the table — answers this.
public sealed record DirectoryUser(string Id, string UserName, IReadOnlyList<string> Roles)
{
    public bool IsAdmin => Roles.Contains(RoleNames.Admin);
}

public interface IUserDirectory
{
    Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default);
    Task<DirectoryUser?> FindByIdAsync(string id, CancellationToken ct = default);

    /// Replaces the user's roles outright. The Identity package models roles as an opaque list on the
    /// account, so which names are legal is the app's call — validated before this is reached.
    Task SetRolesAsync(string id, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// Deletes the account and everything hanging off it. Access grants are the caller's to clear;
    /// they aren't identity data.
    Task RemoveAsync(string id, CancellationToken ct = default);
}
