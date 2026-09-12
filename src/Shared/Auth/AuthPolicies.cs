namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>
/// Policy names so the Host and client use the exact same strings — Host enforces them, client
/// mirrors them in the UI. Just the names live here; each side wires up the actual policy.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Requires the <see cref="Roles.Admin"/> role.</summary>
    public const string AdminOnly = "AdminOnly";
}
