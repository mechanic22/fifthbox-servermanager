namespace FifthBox.ServerManager.Shared.Teams;

public record TeamResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<TeamMemberResponse> Members { get; init; } = [];

    /// every member inherits these
    public int GrantCount { get; init; }
}

public record TeamMemberResponse
{
    public required string Id { get; init; }
    public required string UserName { get; init; }

    /// admins gain nothing from a team, but we keep membership so a demote restores their access
    public bool IsAdmin { get; init; }
}
