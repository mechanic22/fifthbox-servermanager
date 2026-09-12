using FifthBox.ServerManager.Shared.Cluster;

namespace FifthBox.ServerManager.App.Cluster;

/// This host's view of the swarm it belongs to (or doesn't). Read live from the backend.
public record SwarmState
{
    public SwarmMembership Membership { get; init; }
    public bool IsInSwarm { get; init; }
    public bool IsManager { get; init; }
    public string? NodeId { get; init; }
    public string? NodeAddress { get; init; }
    public int NodeCount { get; init; }
    public int ManagerCount { get; init; }
    public string? Error { get; init; }
}

/// The tokens a manager hands out so other machines can join, plus the manager's advertise address.
public record SwarmJoinTokens(string Worker, string Manager, string? ManagerAddress);
