namespace FifthBox.ServerManager.Shared.Workloads;

/// Where a task is in its lifecycle. Docker's own set, which runs forward to Running and then on to one
/// of the terminal states — a task never goes back.
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

/// One replica attempt. Docker keeps these as history, so a service that has restarted twice has three —
/// which is exactly what answers "why is it 0/1": the node it was placed on, how far it got, and what
/// went wrong.
public record WorkloadTask
{
    public required string Id { get; init; }

    /// Which replica this is an attempt at. Repeated across a task's restarts.
    public int Slot { get; init; }

    /// The swarm node it was placed on, or null while the scheduler hasn't placed it.
    public string? NodeId { get; init; }

    public WorkloadTaskState State { get; init; }
    public WorkloadTaskState DesiredState { get; init; }

    /// Docker's progress note ("started", "pending task scheduling").
    public string? Message { get; init; }

    /// Why it isn't running — "no suitable node (insufficient resources)", "No such image". This is the
    /// answer the UI came for.
    public string? Error { get; init; }

    public DateTimeOffset? Since { get; init; }
}
