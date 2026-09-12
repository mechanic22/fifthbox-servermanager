namespace FifthBox.ServerManager.Shared.Teams;

/// Replaces the team's roster outright — the ids that survive are the ones sent.
public class SetTeamMembersRequest
{
    public List<string> UserIds { get; set; } = [];
}
