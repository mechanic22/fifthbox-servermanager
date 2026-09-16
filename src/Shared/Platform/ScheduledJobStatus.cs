namespace FifthBox.ServerManager.Shared.Platform;

public record ScheduledJobStatus
{
    public required string Name { get; init; }
    public int IntervalSeconds { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset? NextRunAt { get; init; }
    public bool Running { get; init; }

    public string? LastError { get; init; }
}
