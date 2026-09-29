using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? GroupId { get; init; }
    public string? GroupName { get; init; }

    public WorkloadDesiredState DesiredState { get; init; }

    public WorkloadTarget Target { get; init; }
    public WorkloadKind Kind { get; init; }
    public string? AgentId { get; init; }
    public string? AgentName { get; init; }

    public string? Image { get; init; }
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public WorkloadPlacement Placement { get; init; }
    public string? NodeId { get; init; }

    /// learned from a running task, only matters with a named volume
    public string? PlacedNodeId { get; init; }

    /// what other workloads dial on the overlay, null for native
    public string? ServiceName { get; init; }

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

    /// minutes past local midnight, null = never
    public int? RestartDailyAtMinutes { get; init; }

    /// runs as CMD-SHELL. blank = no probe, so swarm rolls a crash-looping deploy forward
    public string? HealthCommand { get; init; }
    public int HealthIntervalSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int HealthRetries { get; init; } = 3;
    public int HealthStartPeriodSeconds { get; init; } = 10;

    public IReadOnlyList<EnvVar> Env { get; init; } = [];

    /// saved config differs from the running revision
    public bool HasPendingChanges { get; init; }

    public int? CurrentRevision { get; init; }

    /// sent but not settled, deploying again now just bounces it twice
    public bool IsDeploying { get; init; }

    /// computed per request so the ui gates on what the server enforces
    public AccessLevel Access { get; init; }

    /// deploying publishes config, so it needs Configure
    public bool CanDeploy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
