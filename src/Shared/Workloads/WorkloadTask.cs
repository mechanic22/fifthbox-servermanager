namespace FifthBox.ServerManager.Shared.Workloads;

/// docker's set, only ever moves forward
public enum WorkloadTaskState
{
    Unknown,
    New,
    Allocated,
    Pending,
    Assigned,
    Accepted,
    Preparing,
    Ready,
    Starting,
    Running,
    Complete,
    Shutdown,
    Failed,
    Rejected,
    Orphaned,
    Remove,
}

/// one replica attempt, docker keeps history so two restarts = three tasks
public record WorkloadTask
{
    public required string Id { get; init; }

    /// same across a replica's restarts
    public int Slot { get; init; }

    /// null until scheduled
    public string? NodeId { get; init; }

    public WorkloadTaskState State { get; init; }
    public WorkloadTaskState DesiredState { get; init; }

    public string? Message { get; init; }

    /// why it isn't running ("no suitable node", "No such image")
    public string? Error { get; init; }

    public DateTimeOffset? Since { get; init; }
}
