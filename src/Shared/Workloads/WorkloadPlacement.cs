namespace FifthBox.ServerManager.Shared.Workloads;

/// How the node a container runs on gets decided. Publishing ports is not part of this — a host-bound
/// port is a node resource, so swarm already refuses to put two tasks of one service on the same node
/// and spreads the replicas by itself.
///
/// Stored revisions serialise this by ordinal, so new members go on the end.
public enum WorkloadPlacement
{
    /// The scheduler picks. If the workload has a named volume, the node it picked the first time is
    /// remembered and re-used from then on, because the data is on that node and nowhere else.
    Auto,

    /// A node chosen up front, for when the hardware is the reason — a disk, a GPU, a licence.
    Node,
}
