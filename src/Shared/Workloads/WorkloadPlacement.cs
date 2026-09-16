namespace FifthBox.ServerManager.Shared.Workloads;

/// stored revisions serialize this by ordinal, append only
public enum WorkloadPlacement
{
    /// with a named volume the first node picked sticks, the data only lives there
    Auto,

    Node,
}
