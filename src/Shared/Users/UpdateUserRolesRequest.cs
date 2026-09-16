namespace FifthBox.ServerManager.Shared.Users;

/// full set, not a delta
public class UpdateUserRolesRequest
{
    public List<string> Roles { get; set; } = [];
}
