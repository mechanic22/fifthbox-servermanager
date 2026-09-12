namespace FifthBox.ServerManager.Shared.Users;

/// One user in the admin list. Deliberately smaller than <see cref="UserResponse"/> — the list needs
/// identity and reach, not profile fields.
public record UserSummaryResponse
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public List<string> Roles { get; init; } = [];

    /// Access grants held. Always 0 for an admin — the role bypasses grants rather than implying them.
    public int GrantCount { get; init; }
}
