using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// a few kept for revert, stored as a json column on the workload
public sealed class WorkloadRevision
{
    public int Number { get; set; }
    public DateTimeOffset DeployedAt { get; set; }

    /// false until the status refresh settles it, swarm can accept an update then roll it back
    /// defaults true so revisions from before this field count as applied
    public bool Applied { get; set; } = true;

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

    public string? HealthCommand { get; set; }
    public int HealthIntervalSeconds { get; set; } = 10;
    public int HealthTimeoutSeconds { get; set; } = 3;
    public int HealthRetries { get; set; } = 3;
    public int HealthStartPeriodSeconds { get; set; } = 10;

    public List<EnvVar> Env { get; set; } = [];
}
