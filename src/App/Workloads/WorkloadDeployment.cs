using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public record WorkloadDeployment
{
    public required string Name { get; init; }
    public WorkloadKind Kind { get; init; }

    public string? AgentId { get; init; }

    public string Image { get; init; } = string.Empty;
    public WorkloadMode Mode { get; init; }
    public int Replicas { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];

    /// operator's pick or the node a volume ties it to, null lets the scheduler pick
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

    /// e.g. fbsm.role=platform for our own infra services
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    public string? Command { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.OnFailure;
    public int StopGraceSeconds { get; init; } = 10;
    public string? StopCommand { get; init; }
    public bool ManagedDirectory { get; init; }
    public WorkloadSourceSpec? Source { get; init; }

    public string? HealthCommand { get; init; }
    public int HealthIntervalSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int HealthRetries { get; init; } = 3;
    public int HealthStartPeriodSeconds { get; init; } = 10;


    public IReadOnlyList<EnvVar> Env { get; init; } = [];
}
