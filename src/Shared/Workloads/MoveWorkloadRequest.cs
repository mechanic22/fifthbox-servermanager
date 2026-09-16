namespace FifthBox.ServerManager.Shared.Workloads;

public record MoveWorkloadRequest
{
    public string AgentId { get; init; } = string.Empty;
}

/// the offline flags just mean "not yet", the agent's reconcile finishes the stop/start when it connects
public record MoveWorkloadResponse
{
    public required WorkloadResponse Workload { get; init; }
    public bool PreviousAgentOffline { get; init; }
    public bool TargetAgentOffline { get; init; }
}
