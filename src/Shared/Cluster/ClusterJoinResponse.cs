namespace FifthBox.ServerManager.Shared.Cluster;

/// How to add a Linux node to the swarm. Only populated when this host is a manager (otherwise it has
/// no join tokens to hand out). A Windows / non-swarm box joins via the agent instead (M6).
public record ClusterJoinResponse
{
    public bool Available { get; init; }
    public string? ManagerAddress { get; init; }
    public string? WorkerJoinCommand { get; init; }
    public string? ManagerJoinCommand { get; init; }
}
