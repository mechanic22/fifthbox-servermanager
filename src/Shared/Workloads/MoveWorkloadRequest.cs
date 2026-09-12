namespace FifthBox.ServerManager.Shared.Workloads;

public record MoveWorkloadRequest
{
    public string AgentId { get; init; } = string.Empty;
}

/// A move is stop-there-then-start-here, so it reports the ends it couldn't reach: an offline previous
/// agent stops the leftover process when it reconnects, an offline target starts the workload when it
/// connects. Both are handled by the agent's reconcile — the flags exist to say "not yet".
public record MoveWorkloadResponse
{
    public required WorkloadResponse Workload { get; init; }
    public bool PreviousAgentOffline { get; init; }
    public bool TargetAgentOffline { get; init; }
}
