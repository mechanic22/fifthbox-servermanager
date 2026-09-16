namespace FifthBox.ServerManager.Shared.Auth;

/// new role? add a matching policy in AuthPolicies too
public static class Roles
{
    public const string Admin = "admin";

    /// anything not in here gets rejected
    public static readonly IReadOnlyList<string> All = [Admin];
}
