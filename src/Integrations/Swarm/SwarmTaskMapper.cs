using Docker.DotNet.Models;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

public static class SwarmTaskMapper
{
    /// crash loops pile up history and the whole status goes over SignalR on every change
    public const int MaxTasks = 10;

    public static IReadOnlyList<WorkloadTask> ToTasks(IEnumerable<TaskResponse> tasks) =>
    [
        .. tasks
            .OrderByDescending(StatusTime)
            .Take(MaxTasks)
            .Select(ToTask)
    ];

    /// only tasks swarm still wants running, so a recovered service stops showing its old failure
    public static string? LastError(IEnumerable<WorkloadTask> tasks) =>
        tasks.FirstOrDefault(t =>
            t.DesiredState == WorkloadTaskState.Running
            && t.State != WorkloadTaskState.Running
            && !string.IsNullOrWhiteSpace(t.Error))?.Error;

    /// Status.Timestamp because that's the date the table shows
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
