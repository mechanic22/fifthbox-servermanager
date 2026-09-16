using FifthBox.ServerManager.Shared.Cluster;

namespace FifthBox.ServerManager.App.Cluster;

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

public record SwarmJoinTokens(string Worker, string Manager, string? ManagerAddress);
