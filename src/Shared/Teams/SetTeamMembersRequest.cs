namespace FifthBox.ServerManager.Shared.Teams;

/// replaces the whole roster
public class SetTeamMembersRequest
{
    public List<string> UserIds { get; set; } = [];
}
