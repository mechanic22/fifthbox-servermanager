namespace FifthBox.ServerManager.App.Access;

/// A named roster of users that grants attach to, so a permission can be given to "the ops team"
/// rather than to five people one at a time. Membership is a plain list of user ids — a user can be
/// in any number of teams, and holding both a personal grant and a team grant keeps the higher level.
public class Team
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
