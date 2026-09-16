using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

public static class AgentStatusMapper
{
    public static WorkloadRuntimeStatus ToRuntimeStatus(string name, AgentWorkloadStatus status) => new()
    {
        Name = name,
        Deployed = status.Running,
        // one process, so replicas is only ever 0 or 1
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
