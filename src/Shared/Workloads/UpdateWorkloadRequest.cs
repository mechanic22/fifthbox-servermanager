namespace FifthBox.ServerManager.Shared.Workloads;

/// name and target can't change after create, only fields for the workload's kind get applied
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

    /// minutes past local midnight, null = never
    public int? RestartDailyAtMinutes { get; init; }

    /// runs as CMD-SHELL. blank = no probe, so swarm rolls a crash-looping deploy forward
    public string? HealthCommand { get; init; }
    public int HealthIntervalSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int HealthRetries { get; init; } = 3;
    public int HealthStartPeriodSeconds { get; init; } = 10;

    public IReadOnlyList<EnvVar> Env { get; init; } = [];
}
