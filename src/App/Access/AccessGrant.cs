using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

/// one row per (subject, scope, target), a re-grant updates the level
public class AccessGrant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public AccessSubject SubjectType { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public AccessScope Scope { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public AccessLevel Level { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
