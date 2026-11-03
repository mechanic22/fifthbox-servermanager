using FifthBox.ServerManager.Shared.Certificates;
using FifthBox.ServerManager.Shared.Cluster;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Components;

public enum StatusTone
{
    Neutral,
    Good,
    Warn,
    Bad,
}

public static class StatusTones
{
    public static StatusTone For(WorkloadState state) => state switch
    {
        WorkloadState.Running => StatusTone.Good,
        WorkloadState.Partial or WorkloadState.Updating => StatusTone.Warn,
        _ => StatusTone.Neutral,
    };

    /// <param name="missing">the service should exist but swarm has lost it</param>
    public static StatusTone For(WorkloadRuntimeStatus status, bool missing = false) => status switch
    {
        _ when missing => StatusTone.Bad,
        { State: WorkloadState.Running } => StatusTone.Good,
        { LastError.Length: > 0 } => StatusTone.Bad,
        _ => For(status.State),
    };

    public static StatusTone For(WorkloadTask task) => task.State switch
    {
        WorkloadTaskState.Running => StatusTone.Good,

        // an old task being shut down isn't a problem whatever state it died in
        _ when task.DesiredState != WorkloadTaskState.Running => StatusTone.Neutral,
        WorkloadTaskState.Failed or WorkloadTaskState.Rejected or WorkloadTaskState.Orphaned => StatusTone.Bad,
        _ => StatusTone.Warn,
    };

    public static StatusTone For(NodeStatus status) => status switch
    {
        NodeStatus.Ready => StatusTone.Good,
        NodeStatus.Down => StatusTone.Bad,
        NodeStatus.Disconnected => StatusTone.Warn,
        _ => StatusTone.Neutral,
    };

    public static StatusTone For(SwarmMembership membership) => membership switch
    {
        SwarmMembership.Active => StatusTone.Good,
        SwarmMembership.Error or SwarmMembership.Locked => StatusTone.Bad,
        SwarmMembership.Pending => StatusTone.Warn,
        _ => StatusTone.Neutral,
    };

    public static StatusTone For(CertificateStatus? status) => status switch
    {
        CertificateStatus.Valid => StatusTone.Good,
        CertificateStatus.Pending => StatusTone.Warn,
        CertificateStatus.Failed => StatusTone.Bad,
        _ => StatusTone.Neutral,
    };
}
