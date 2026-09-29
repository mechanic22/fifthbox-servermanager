namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadRevisionResponse
{
    public required int Number { get; init; }
    public DateTimeOffset DeployedAt { get; init; }

    /// newest revision, what's running
    public bool IsCurrent { get; init; }

    /// like "nginx:1.27 ×2" or "dotnet run"
    public string Summary { get; init; } = string.Empty;

    public string? Image { get; init; }
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public WorkloadPlacement Placement { get; init; }
    public string? NodeId { get; init; }
    public int? MemoryLimitMb { get; init; }
    public double? CpuLimit { get; init; }

    public int? MemoryReserveMb { get; init; }
    public double? CpuReserve { get; init; }
    public IReadOnlyList<VolumeMount> Mounts { get; init; } = [];
    public string? Command { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public RestartPolicy RestartPolicy { get; init; }
    public int StopGraceSeconds { get; init; }
    public string? StopCommand { get; init; }
    public bool ManagedDirectory { get; init; }
    public WorkloadSourceResponse? Source { get; init; }

    /// runs as CMD-SHELL. blank = no probe, so swarm rolls a crash-looping deploy forward
    public string? HealthCommand { get; init; }
    public int HealthIntervalSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int HealthRetries { get; init; } = 3;
    public int HealthStartPeriodSeconds { get; init; } = 10;

    /// empty below Configure, and secret values are always blank
    public IReadOnlyList<EnvVar> Env { get; init; } = [];
}
