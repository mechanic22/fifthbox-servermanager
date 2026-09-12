using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// A user-defined deployable — the desired state, persisted by Storage. Polymorphic: a Container runs on
/// the swarm (image/replicas/ports); a Native workload runs as a process on a specific agent
/// (command/args/working dir). The target picks the kind. Observed runtime state is read from the backend.
public class Workload
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;

    /// Optional group this workload belongs to (null = root).
    public string? GroupId { get; set; }

    /// Set by Deploy and Stop, never by the backend. A workload the operator stopped and one whose
    /// service somebody deleted out of band both report nothing running; only this tells them apart.
    public WorkloadDesiredState DesiredState { get; set; }

    public WorkloadTarget Target { get; set; }
    public WorkloadKind Kind { get; set; }

    /// Set when Target is Agent — the agent this native workload runs on.
    public string? AgentId { get; set; }

    // Container (swarm) config.
    public string? Image { get; set; }
    /// Replicated runs Replicas copies; Global runs one per node and ignores Replicas entirely.
    public WorkloadMode Mode { get; set; }

    public int Replicas { get; set; } = 1;
    public List<PortMapping> Ports { get; set; } = [];

    /// How the node gets decided.
    public WorkloadPlacement Placement { get; set; } = WorkloadPlacement.Auto;

    /// The node an operator picked, set when Placement is Node.
    public string? NodeId { get; set; }

    /// Where the scheduler actually put it, learned once from a running task and then kept. Only
    /// meaningful for a workload with a named volume: the data is on that node and nowhere else, so
    /// every later deploy has to go back to it rather than start on an empty one.
    public string? PlacedNodeId { get; set; }

    /// Container resource limits (null = unlimited). Memory in MiB; CPU in cores (0.5 = half a core).
    public int? MemoryLimitMb { get; set; }
    public double? CpuLimit { get; set; }

    /// What the scheduler sets aside for this workload on whichever node it lands. Limits cap a running
    /// container; reservations are what swarm actually places on — with none set every node looks free,
    /// so it will happily pack more work onto a box than the box has, and the limit then OOM-kills it.
    public int? MemoryReserveMb { get; set; }
    public double? CpuReserve { get; set; }

    /// Persistent data: named volumes + host bind mounts.
    public List<VolumeMount> Mounts { get; set; } = [];

    // Native (agent) config.
    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];
    public string? WorkingDirectory { get; set; }

    /// What the agent does when the process exits on its own. Swarm restarts its own failed tasks, so
    /// this only bites for native workloads.
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;

    /// Seconds between asking the process to terminate and killing it.
    public int StopGraceSeconds { get; set; } = 10;

    /// Console command that asks the process to shut down cleanly, written to its stdin before any
    /// signal. Blank means go straight to signalling — which on Windows means straight to a kill.
    public string? StopCommand { get; set; }

    /// Let the agent own the workload's directory rather than pointing at somewhere hand-installed.
    /// Turning it on makes WorkingDirectory redundant and is what later lets content be fetched into a
    /// known place.
    public bool ManagedDirectory { get; set; }

    /// Minutes past local midnight to bounce this workload every day, or null for never. Operational,
    /// not deployed config: it is deliberately absent from the revision snapshot and the config
    /// signature, so changing it never asks for a redeploy.
    public int? RestartDailyAtMinutes { get; set; }

    /// When the scheduled restart last fired, so a job tick can tell today's from yesterday's.
    public DateTimeOffset? LastScheduledRestartAt { get; set; }

    /// Where the agent gets this workload's files from. Anything other than None needs
    /// <see cref="ManagedDirectory"/>, because the agent has to own the directory it writes into.
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

    /// Deployed config history (newest = what's running). Captured on deploy, capped at the last few.
    public List<WorkloadRevision> Revisions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
