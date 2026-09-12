namespace FifthBox.ServerManager.Shared.Users;

/// Replaces a user's roles outright — send the full set, not a delta.
public class UpdateUserRolesRequest
{
    public List<string> Roles { get; set; } = [];
}
