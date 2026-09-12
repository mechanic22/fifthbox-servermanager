namespace FifthBox.ServerManager.Shared.Cluster;

/// This host's swarm membership (Docker's LocalNodeState).
public enum SwarmMembership
{
    Unknown,
    Inactive,
    Pending,
    Active,
    Error,
    Locked,
}
