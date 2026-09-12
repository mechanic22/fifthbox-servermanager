namespace FifthBox.ServerManager.Shared.Workloads;

/// Name and target are immutable after creation; everything else can change. The service applies only
/// the fields relevant to the workload's kind.
public record UpdateWorkloadRequest
{
    public string? GroupId { get; init; }

    public string Image { get; init; } = string.Empty;
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; } = 1;
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public WorkloadPlacement Placement { get; init; } = WorkloadPlacement.Auto;
    public string? NodeId { get; init; }
    public int? MemoryLimitMb { get; init; }
    public double? CpuLimit { get; init; }

    public int? MemoryReserveMb { get; init; }
    public double? CpuReserve { get; init; }
    public IReadOnlyList<VolumeMount> Mounts { get; init; } = [];

    public string Command { get; init; } = string.Empty;
    public IReadOnlyList<string> Args { get; init; } = [];
    public string WorkingDirectory { get; init; } = string.Empty;
    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.OnFailure;
    public int StopGraceSeconds { get; init; } = 10;
    public string? StopCommand { get; init; }
    public bool ManagedDirectory { get; init; }
    public WorkloadSourceRequest? Source { get; init; }

    /// Minutes past local midnight for a daily restart, or null for never.
    public int? RestartDailyAtMinutes { get; init; }

    /// Container health probe, run inside the container as `CMD-SHELL`. Blank means no probe — and
    /// without one swarm calls a deploy successful the moment the process starts, so a container that
    /// starts and immediately crash-loops rolls forward instead of back.
    public string? HealthCommand { get; init; }
    public int HealthIntervalSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int HealthRetries { get; init; } = 3;
    public int HealthStartPeriodSeconds { get; init; } = 10;

    public IReadOnlyList<EnvVar> Env { get; init; } = [];
}
