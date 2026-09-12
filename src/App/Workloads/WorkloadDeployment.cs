using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// A workload prepared for a backend to run. Carries everything both backends need: the swarm/container
/// fields and the agent/native fields, plus the target identity. The App builds this from a Workload; a
/// backend uses the parts relevant to its kind.
public record WorkloadDeployment
{
    public required string Name { get; init; }
    public WorkloadKind Kind { get; init; }

    /// The agent this native workload runs on (null for container/swarm workloads).
    public string? AgentId { get; init; }

    // Container (swarm) fields.
    public string Image { get; init; } = string.Empty;
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];

    /// The node this must run on — an operator's choice, or the one a volume ties it to. Null lets the
    /// scheduler pick.
    public string? NodeId { get; init; }
    public int? MemoryLimitMb { get; init; }
    public double? CpuLimit { get; init; }
    public int? MemoryReserveMb { get; init; }
    public double? CpuReserve { get; init; }
    public IReadOnlyList<VolumeMount> Mounts { get; init; } = [];
    public string? Network { get; init; }
    public IReadOnlyList<ConfigMount> Configs { get; init; } = [];
    public IReadOnlyList<SecretMount> Secrets { get; init; } = [];
    public bool PinToControlNode { get; init; }

    /// Service labels (e.g. fbsm.role=platform to mark ServerManager's own infrastructure services).
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    // Native (agent) fields.
    public string? Command { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.OnFailure;
    public int StopGraceSeconds { get; init; } = 10;
    public string? StopCommand { get; init; }
    public bool ManagedDirectory { get; init; }
    public WorkloadSourceSpec? Source { get; init; }

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
