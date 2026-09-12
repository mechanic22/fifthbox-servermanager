namespace FifthBox.ServerManager.Shared.Workloads;

/// The observed runtime state of a workload on its backend — distinct from the persisted definition.
public record WorkloadRuntimeStatus
{
    public required string Name { get; init; }
    public bool Deployed { get; init; }
    public int DesiredReplicas { get; init; }
    public int RunningReplicas { get; init; }
    public WorkloadState State { get; init; }

    /// Swarm's own view of the last rollout ("updating", "completed", "rolled_back"). Without this a
    /// failed deploy that swarm quietly reverted looks identical to a successful one.
    public string? UpdateState { get; init; }
    public string? UpdateMessage { get; init; }

    /// The rollout stopped without deciding — swarm pauses an update whose task failed when there's no
    /// health check to roll it back. Nothing else is coming, so the deploy controls have to open back up.
    public bool RolloutStalled { get; init; }

    /// The recent replica attempts, newest first — swarm's own task history, capped. Empty for native
    /// workloads, whose single process is described by the fields below instead.
    public IReadOnlyList<WorkloadTask> Tasks { get; init; } = [];

    /// The newest error from a task that isn't running, so a list row can say why without carrying the
    /// whole task history.
    public string? LastError { get; init; }

    /// The image the running tasks were actually started from, digest and all. Swarm resolves a tag to a
    /// digest when it deploys, so this is the only way to tell whether a redeploy of ":latest" picked up
    /// a new build or the same one.
    public string? RunningImage { get; init; }

    // Native (agent) detail — swarm workloads leave these null/zero.
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
