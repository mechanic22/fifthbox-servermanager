using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// A point-in-time snapshot of a workload's deployable config, captured on deploy. The newest revision is
/// the config the running instance uses; a few older ones are kept so a workload can be reverted. Stored
/// as a JSON column on the workload — not a separate table.
public sealed class WorkloadRevision
{
    public int Number { get; set; }
    public DateTimeOffset DeployedAt { get; set; }

    /// Whether the backend actually kept this config. A deploy records it false and the status refresh
    /// settles it, because swarm can accept an update and then roll it back — and a revision the cluster
    /// reverted is not the one running.
    ///
    /// Defaults true so revisions written before this field existed keep counting as applied; the deploy
    /// path sets it false explicitly.
    public bool Applied { get; set; } = true;

    // Snapshot of the deployable config (the subset that actually reaches the backend).
    public string? Image { get; set; }
    public WorkloadMode Mode { get; set; }
    public int Replicas { get; set; }
    public List<PortMapping> Ports { get; set; } = [];
    public WorkloadPlacement Placement { get; set; } = WorkloadPlacement.Auto;
    public string? NodeId { get; set; }
    public int? MemoryLimitMb { get; set; }
    public double? CpuLimit { get; set; }
    public int? MemoryReserveMb { get; set; }
    public double? CpuReserve { get; set; }
    public List<VolumeMount> Mounts { get; set; } = [];
    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];
    public string? WorkingDirectory { get; set; }
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;
    public int StopGraceSeconds { get; set; } = 10;
    public string? StopCommand { get; set; }
    public bool ManagedDirectory { get; set; }
    public WorkloadSource Source { get; set; } = new();

    /// Container health probe, run inside the container as `CMD-SHELL`. Blank means no probe — and
    /// without one swarm calls a deploy successful the moment the process starts, so a container that
    /// starts and immediately crash-loops rolls forward instead of back.
    public string? HealthCommand { get; set; }
    public int HealthIntervalSeconds { get; set; } = 10;
    public int HealthTimeoutSeconds { get; set; } = 3;
    public int HealthRetries { get; set; } = 3;
    public int HealthStartPeriodSeconds { get; set; } = 10;

    public List<EnvVar> Env { get; set; } = [];
}
