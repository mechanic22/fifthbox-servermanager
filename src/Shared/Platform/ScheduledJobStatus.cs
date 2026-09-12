namespace FifthBox.ServerManager.Shared.Platform;

/// A background job and how its last run went.
public record ScheduledJobStatus
{
    public required string Name { get; init; }
    public int IntervalSeconds { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public bool Running { get; init; }

    /// The failure from the last attempt, or null if it succeeded (or hasn't run yet).
    public string? LastError { get; init; }
}
