namespace FifthBox.ServerManager.Shared.Users;

public record UserSummaryResponse
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public List<string> Roles { get; init; } = [];

    /// always 0 for admins, the role bypasses grants
    public int GrantCount { get; init; }
}
