using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Cluster;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm;

public static class SwarmStateMapper
{
    public static SwarmState ToState(DockerModels.Info swarm) => new()
    {
        Membership = ParseMembership(swarm.LocalNodeState),
        IsInSwarm = string.Equals(swarm.LocalNodeState, "active", StringComparison.OrdinalIgnoreCase),
        IsManager = swarm.ControlAvailable,
        NodeId = string.IsNullOrEmpty(swarm.NodeID) ? null : swarm.NodeID,
        NodeAddress = string.IsNullOrEmpty(swarm.NodeAddr) ? null : swarm.NodeAddr,
        NodeCount = (int)swarm.Nodes,
        ManagerCount = (int)swarm.Managers,
        Error = string.IsNullOrEmpty(swarm.Error) ? null : swarm.Error,
    };

    private static SwarmMembership ParseMembership(string? state) => state?.ToLowerInvariant() switch
    {
        "inactive" => SwarmMembership.Inactive,
        "pending" => SwarmMembership.Pending,
        "active" => SwarmMembership.Active,
        "error" => SwarmMembership.Error,
        "locked" => SwarmMembership.Locked,
        _ => SwarmMembership.Unknown,
    };
}
