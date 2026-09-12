using Docker.DotNet.Models;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Projects docker's task list into the replica history a workload shows. Pure, and the reason the
/// status calls stopped throwing this away: a service stuck at 0/1 is only explicable from its tasks.
public static class SwarmTaskMapper
{
    /// A crash-looping service accumulates task history without limit, and the whole status goes over
    /// SignalR on every change.
    public const int MaxTasks = 10;

    public static IReadOnlyList<WorkloadTask> ToTasks(IEnumerable<TaskResponse> tasks) =>
    [
        .. tasks
            .OrderByDescending(StatusTime)
            .Take(MaxTasks)
            .Select(ToTask)
    ];

    /// The newest error from a task swarm still wants running. Superseded attempts are marked desired
    /// Shutdown, so a service that recovered stops reporting the failure it recovered from — which a
    /// plain "newest error" would show forever.
    public static string? LastError(IEnumerable<WorkloadTask> tasks) =>
        tasks.FirstOrDefault(t =>
            t.DesiredState == WorkloadTaskState.Running
            && t.State != WorkloadTaskState.Running
            && !string.IsNullOrWhiteSpace(t.Error))?.Error;

    /// The table shows Status.Timestamp, so ordering on anything else (UpdatedAt is close but not equal)
    /// lists rows out of order by the only date the reader can see.
    private static DateTime StatusTime(TaskResponse t) =>
        t.Status?.Timestamp is { } stamp && stamp != default ? stamp : t.UpdatedAt;

    private static WorkloadTask ToTask(TaskResponse t) => new()
    {
        Id = t.ID ?? string.Empty,
        Slot = (int)t.Slot,
        NodeId = string.IsNullOrEmpty(t.NodeID) ? null : t.NodeID,
        State = ParseState(t.Status?.State),
        DesiredState = ParseState(t.DesiredState),
        Message = string.IsNullOrWhiteSpace(t.Status?.Message) ? null : t.Status!.Message,
        Error = string.IsNullOrWhiteSpace(t.Status?.Err) ? null : t.Status!.Err,
        Since = t.Status?.Timestamp == default ? null : t.Status!.Timestamp,
    };

    private static WorkloadTaskState ParseState(TaskState? state) => state switch
    {
        TaskState.New => WorkloadTaskState.New,
        TaskState.Allocated => WorkloadTaskState.Allocated,
        TaskState.Pending => WorkloadTaskState.Pending,
        TaskState.Assigned => WorkloadTaskState.Assigned,
        TaskState.Accepted => WorkloadTaskState.Accepted,
        TaskState.Preparing => WorkloadTaskState.Preparing,
        TaskState.Ready => WorkloadTaskState.Ready,
        TaskState.Starting => WorkloadTaskState.Starting,
        TaskState.Running => WorkloadTaskState.Running,
        TaskState.Complete => WorkloadTaskState.Complete,
        TaskState.Shutdown => WorkloadTaskState.Shutdown,
        TaskState.Failed => WorkloadTaskState.Failed,
        TaskState.Rejected => WorkloadTaskState.Rejected,
        TaskState.Orphaned => WorkloadTaskState.Orphaned,
        TaskState.Remove => WorkloadTaskState.Remove,
        _ => WorkloadTaskState.Unknown,
    };
}
