namespace FifthBox.ServerManager.Shared.Teams;

public record TeamResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<TeamMemberResponse> Members { get; init; } = [];

    /// Access grants held by the team. Every member inherits all of them.
    public int GrantCount { get; init; }
}

public record TeamMemberResponse
{
    public required string Id { get; init; }
    public required string UserName { get; init; }

    /// An admin member gains nothing from the team — the role already bypasses grants. Membership is
    /// still kept, so demoting them restores exactly the access the roster says they should have.
    public bool IsAdmin { get; init; }
}
