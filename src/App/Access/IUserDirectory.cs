using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Access;

/// identity package can't enumerate accounts, so storage answers this
public sealed record DirectoryUser(string Id, string UserName, IReadOnlyList<string> Roles)
{
    public bool IsAdmin => Roles.Contains(RoleNames.Admin);
}

public interface IUserDirectory
{
    Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default);
    Task<DirectoryUser?> FindByIdAsync(string id, CancellationToken ct = default);

    /// replaces roles outright, names are validated before this
    Task SetRolesAsync(string id, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// clearing access grants is on the caller, they aren't identity data
    Task RemoveAsync(string id, CancellationToken ct = default);
}
