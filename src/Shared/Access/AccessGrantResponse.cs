namespace FifthBox.ServerManager.Shared.Access;

public record AccessGrantResponse
{
    public required string Id { get; init; }
    public AccessSubject SubjectType { get; init; }
    public required string SubjectId { get; init; }

    /// The user name or team name, resolved for display.
    public required string SubjectName { get; init; }

    public AccessScope Scope { get; init; }
    public required string TargetId { get; init; }

    /// The group or workload name, resolved for display. Empty when the target has been deleted and
    /// the grant hasn't been cleaned up yet.
    public string TargetName { get; init; } = string.Empty;

    public AccessLevel Level { get; init; }

    /// True when this grant sits on an ancestor group rather than on the thing being asked about, so
    /// it reaches here by inheritance. Only ever set when listing one target's access.
    public bool Inherited { get; init; }
}
