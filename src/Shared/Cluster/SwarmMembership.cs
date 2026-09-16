namespace FifthBox.ServerManager.Shared.Cluster;

/// docker's LocalNodeState
public enum SwarmMembership
{
    Unknown,
    Inactive,
    Pending,
    Active,
    Error,
    Locked,
}
