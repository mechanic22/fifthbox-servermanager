namespace FifthBox.ServerManager.Shared.Access;

public record AccessGrantResponse
{
    public required string Id { get; init; }
    public AccessSubject SubjectType { get; init; }
    public required string SubjectId { get; init; }

    public required string SubjectName { get; init; }

    public AccessScope Scope { get; init; }
    public required string TargetId { get; init; }

    /// empty when the target's gone but the grant hasn't been cleaned up yet
    public string TargetName { get; init; } = string.Empty;

    public AccessLevel Level { get; init; }

    /// grant is on an ancestor group, only set when listing one target's access
    public bool Inherited { get; init; }
}
