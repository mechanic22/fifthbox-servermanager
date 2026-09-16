namespace FifthBox.ServerManager.Shared.Workloads;

public enum WorkloadState
{
    NotDeployed,
    Running,
    Partial,
    Stopped,

    /// native only, acquiring files and won't run until done
    Updating,
}
