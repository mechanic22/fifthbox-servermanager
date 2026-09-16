using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public class Workload
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;

    /// null = root
    public string? GroupId { get; set; }

    /// set by Deploy and Stop only, tells an operator stop apart from a service deleted out of band
    public WorkloadDesiredState DesiredState { get; set; }

    public WorkloadTarget Target { get; set; }
    public WorkloadKind Kind { get; set; }

    public string? AgentId { get; set; }

    public string? Image { get; set; }
    /// Global runs one per node and ignores Replicas
    public WorkloadMode Mode { get; set; }

    public int Replicas { get; set; } = 1;
    public List<PortMapping> Ports { get; set; } = [];

    public WorkloadPlacement Placement { get; set; } = WorkloadPlacement.Auto;

    /// set when Placement is Node
    public string? NodeId { get; set; }

    /// learned once from a running task and kept, a named volume's data only lives on that node
    public string? PlacedNodeId { get; set; }

    /// null = unlimited. memory in MiB, cpu in cores (0.5 = half)
    public int? MemoryLimitMb { get; set; }
    public double? CpuLimit { get; set; }

    /// what swarm actually places on, with none set it overpacks a node and the limit then OOM-kills
    public int? MemoryReserveMb { get; set; }
    public double? CpuReserve { get; set; }

    public List<VolumeMount> Mounts { get; set; } = [];

    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];
    public string? WorkingDirectory { get; set; }

    /// only bites native workloads, swarm restarts its own failed tasks
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;

    public int StopGraceSeconds { get; set; } = 10;

    /// written to stdin before any signal, blank goes straight to a signal (a kill on windows)
    public string? StopCommand { get; set; }

    /// agent owns the dir instead of a hand-installed path, makes WorkingDirectory redundant
    public bool ManagedDirectory { get; set; }

    /// minutes past local midnight, null for never
    /// not in the revision or signature on purpose, so changing it never asks for a redeploy
    public int? RestartDailyAtMinutes { get; set; }

    public DateTimeOffset? LastScheduledRestartAt { get; set; }

    /// anything but None needs ManagedDirectory, the agent has to own where it writes
    public WorkloadSource Source { get; set; } = new();

    /// runs as CMD-SHELL, without one a crash-looping container rolls forward instead of back
    public string? HealthCommand { get; set; }
    public int HealthIntervalSeconds { get; set; } = 10;
    public int HealthTimeoutSeconds { get; set; } = 3;
    public int HealthRetries { get; set; } = 3;
    public int HealthStartPeriodSeconds { get; set; } = 10;


    public List<EnvVar> Env { get; set; } = [];

    /// captured on deploy, capped at the last few
    public List<WorkloadRevision> Revisions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
