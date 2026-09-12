using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

/// One translation of an agent's process report into the runtime status clients see, shared by both
/// directions: the backend asking an agent, and the agent pushing unprompted.
public static class AgentStatusMapper
{
    public static WorkloadRuntimeStatus ToRuntimeStatus(string name, AgentWorkloadStatus status) => new()
    {
        Name = name,
        Deployed = status.Running,
        // A native workload is one process, so "replicas" only ever reads as 0 or 1.
        DesiredReplicas = 1,
        RunningReplicas = status.Running ? 1 : 0,
        State = status.Updating ? WorkloadState.Updating
            : status.Running ? WorkloadState.Running
            : WorkloadState.Stopped,
        Pid = status.Pid,
        StartedAt = status.StartedAt,
        RestartCount = status.RestartCount,
        ExitCode = status.ExitCode,
        Adopted = status.Adopted,
        Detail = status.Detail,
        Updating = status.Updating,
        InstalledVersion = status.InstalledVersion,
        MemoryBytes = status.MemoryBytes,
        CpuPercent = status.CpuPercent,
        Reachable = status.Reachable,
    };
}
