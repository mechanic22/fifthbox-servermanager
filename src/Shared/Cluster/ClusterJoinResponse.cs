namespace FifthBox.ServerManager.Shared.Cluster;

/// only filled in on a manager, windows / non-swarm boxes join via the agent
public record ClusterJoinResponse
{
    public bool Available { get; init; }
    public string? ManagerAddress { get; init; }
    public string? WorkerJoinCommand { get; init; }
    public string? ManagerJoinCommand { get; init; }
}
