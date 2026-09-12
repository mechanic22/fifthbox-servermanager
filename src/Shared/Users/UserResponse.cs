namespace FifthBox.ServerManager.Shared.Users;

/// <summary>
/// One user as the app sees it: identity bits (id, username, roles, linked providers) plus profile
/// bits (email, name). Host stitches it together from Identity and the profile store — the client
/// just sees one user.
/// </summary>
public class UserResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>External providers linked to this account (e.g. "Google").</summary>
    public List<string> LinkedProviders { get; set; } = [];
}
