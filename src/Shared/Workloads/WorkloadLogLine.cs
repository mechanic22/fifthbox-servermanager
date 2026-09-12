namespace FifthBox.ServerManager.Shared.Workloads;

public enum LogStream
{
    Stdout,
    Stderr,
}

/// One line of a workload's output. Timestamp is whatever the source reported — Docker stamps its own,
/// an agent stamps on capture — so it is best-effort ordering, not a guarantee.
public record WorkloadLogLine
{
    public required string Text { get; init; }
    public LogStream Stream { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}
