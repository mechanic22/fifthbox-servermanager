namespace FifthBox.ServerManager.Shared.Nodes;

/// Derived health of a node — the swarm's view of whether it's usable.
public enum NodeStatus
{
    Unknown,
    Ready,
    Down,
    Disconnected,
}
