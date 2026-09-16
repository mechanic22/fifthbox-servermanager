namespace FifthBox.ServerManager.Shared.Workloads;

public enum LogStream
{
    Stdout,
    Stderr,
}

/// Timestamp is best effort, docker stamps its own and agents stamp on capture
public record WorkloadLogLine
{
    public required string Text { get; init; }
    public LogStream Stream { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}
