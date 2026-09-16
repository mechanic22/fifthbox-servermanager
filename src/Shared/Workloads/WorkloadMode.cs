namespace FifthBox.ServerManager.Shared.Workloads;

/// containers only
public enum WorkloadMode
{
    Replicated,

    /// one per schedulable node, including ones that join later. Replicas is ignored
    Global,
}
