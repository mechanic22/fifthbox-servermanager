using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? GroupId { get; init; }
    public string? GroupName { get; init; }

    /// What was last asked for. With the runtime status it distinguishes "stopped" from "gone".
    public WorkloadDesiredState DesiredState { get; init; }

    public WorkloadTarget Target { get; init; }
    public WorkloadKind Kind { get; init; }
    public string? AgentId { get; init; }
    public string? AgentName { get; init; }

    // Container (swarm) config.
    public string? Image { get; init; }
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public WorkloadPlacement Placement { get; init; }
    public string? NodeId { get; init; }

    /// Where the scheduler actually put it, learned from a running task. Only meaningful for a workload
    /// with a named volume, whose data is on that node and nowhere else.
    public string? PlacedNodeId { get; init; }

    /// What other workloads dial on the overlay. Null for native workloads, which aren't on it.
    public string? ServiceName { get; init; }

    public int? MemoryLimitMb { get; init; }
    public double? CpuLimit { get; init; }

    public int? MemoryReserveMb { get; init; }
    public double? CpuReserve { get; init; }
    public IReadOnlyList<VolumeMount> Mounts { get; init; } = [];

    // Native (agent) config.
    public string? Command { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public RestartPolicy RestartPolicy { get; init; }
    public int StopGraceSeconds { get; init; }
    public string? StopCommand { get; init; }
    public bool ManagedDirectory { get; init; }
    public WorkloadSourceResponse? Source { get; init; }

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

    /// The saved config differs from the running (newest) revision — a redeploy would apply it.
    public bool HasPendingChanges { get; init; }

    /// The running revision number, or null if never deployed.
    public int? CurrentRevision { get; init; }

    /// A revision has been sent to the backend and hasn't settled yet. The deploy is still happening —
    /// pressing Deploy again during this window only bounces the workload a second time.
    public bool IsDeploying { get; init; }

    /// What the caller who asked may do with this. Server-computed per request — the client gates its
    /// controls on the same number the server enforces.
    public AccessLevel Access { get; init; }

    /// Whether this caller may deploy right now. Server-computed because it isn't a plain level
    /// comparison — Operate may only bounce a workload that's already running exactly what's saved.
    public bool CanDeploy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
