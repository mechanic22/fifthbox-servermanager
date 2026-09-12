namespace FifthBox.ServerManager.Shared.Workloads;

public enum WorkloadState
{
    NotDeployed,
    Running,
    Partial,
    Stopped,

    /// Native only: the agent is acquiring this workload's files and won't run it until it's done.
    Updating,
}
