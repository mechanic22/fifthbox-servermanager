namespace FifthBox.ServerManager.Shared.Cluster;

public record ClusterStatusResponse
{
    public SwarmMembership Membership { get; init; }
    public bool IsInSwarm { get; init; }
    public bool IsManager { get; init; }
    public string? NodeId { get; init; }
    public string? NodeAddress { get; init; }
    public int NodeCount { get; init; }
    public int ManagerCount { get; init; }
    public string? Error { get; init; }
    public string ManagedNetworkName { get; init; } = string.Empty;
    public bool ManagedNetworkExists { get; init; }
}
