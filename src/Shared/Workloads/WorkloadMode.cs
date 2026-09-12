namespace FifthBox.ServerManager.Shared.Workloads;

/// How many copies the scheduler runs. Container workloads only — a native workload is one process on
/// one agent by definition.
public enum WorkloadMode
{
    /// A fixed number of copies, placed wherever they fit.
    Replicated,

    /// Exactly one on every node that will take work, and one on each node that joins later. Replicas
    /// mean nothing here — the cluster's size is the count.
    Global,
}
