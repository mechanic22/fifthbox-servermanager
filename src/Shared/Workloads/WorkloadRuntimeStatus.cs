namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadRuntimeStatus
{
    public required string Name { get; init; }
    public bool Deployed { get; init; }
    public int DesiredReplicas { get; init; }
    public int RunningReplicas { get; init; }
    public WorkloadState State { get; init; }

    /// without it a deploy swarm quietly rolled back looks like a success
    public string? UpdateState { get; init; }
    public string? UpdateMessage { get; init; }

    /// swarm paused a failed update it couldn't roll back (no health check), unlock deploy
    public bool RolloutStalled { get; init; }

    /// newest first, capped. empty for native
    public IReadOnlyList<WorkloadTask> Tasks { get; init; } = [];

    /// newest error from a non-running task, so a list row can say why
    public string? LastError { get; init; }

    /// with digest, only way to tell if redeploying :latest actually got a new build
    public string? RunningImage { get; init; }

    // native only, swarm leaves these null/zero
    public int? Pid { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public int RestartCount { get; init; }
    public int? ExitCode { get; init; }
    public bool Adopted { get; init; }
    public string? Detail { get; init; }
    public bool Updating { get; init; }
    public string? InstalledVersion { get; init; }
    public long? MemoryBytes { get; init; }
    public double? CpuPercent { get; init; }
    public bool? Reachable { get; init; }
}
