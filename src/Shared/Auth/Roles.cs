namespace FifthBox.ServerManager.Shared.Auth;

/// <summary>
/// The app's role names, shared by Host (policies, seeding) and client (UI gating). Identity keeps
/// roles as opaque strings, so this is where the app pins the actual names.
/// Add a role: add a const here, then a matching policy in <see cref="AuthPolicies"/>.
/// </summary>
public static class Roles
{
    public const string Admin = "admin";

    /// Every role the app recognises. Anything else is rejected rather than stored as a role that
    /// no policy will ever match.
    public static readonly IReadOnlyList<string> All = [Admin];
}
