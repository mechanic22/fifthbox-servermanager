using System.Diagnostics;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

/// What the agent knows about one native workload it is running. The spec is kept alongside the process
/// because both stopping (grace period) and restarting (policy) need it after the original command is gone.
public sealed class SupervisedWorkload
{
    /// Guards every transition of this workload — start, exit, restart, stop — so the process exit
    /// callback can't interleave with an operator command.
    public Lock Gate { get; } = new();

    public required AgentWorkloadSpec Spec { get; set; }

    /// `ProcessAdoption.HashOf` for the authoritative spec — kept separately because a spec restored from
    /// the state file has had its env removed and would no longer hash to the same value.
    public required string ConfigHash { get; set; }
    public Process? Process { get; set; }

    /// The running process's stdin, for console commands. Null for an adopted process — its pipes belong
    /// to whoever started it, so there is nothing to write to.
    public StreamWriter? Input { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public int? LastExitCode { get; set; }

    /// Set before we signal the process, so the exit callback can tell an operator stop from a crash.
    public bool StopRequested { get; set; }

    /// Automatic restarts in the current crash run. Reset when an operator deploys, and when a run lasts
    /// long enough to count as a recovery.
    public int RestartCount { get; set; }

    /// The agent attached to an already-running process rather than starting one.
    public bool Adopted { get; set; }

    /// An acquire is in flight. Nothing may start while this is set: rewriting binaries under a live
    /// process corrupts the install, which is why the two states are exclusive rather than merely
    /// discouraged.
    public bool Updating { get; set; }

    /// Captured stdout/stderr. Survives restarts of the process, not of the agent — and stays empty for
    /// an adopted process, whose pipes belong to whoever started it.
    public LogBuffer Logs { get; } = new(LogCapacity);

    public const int LogCapacity = 500;

    /// What the last successful acquire put on disk, read back from the marker in the install root.
    public string? InstalledVersion { get; set; }

    /// Last CPU sample, so the next one can be turned into a percentage. CPU is only meaningful as a
    /// delta between two readings.
    public TimeSpan? LastCpu { get; set; }
    public DateTimeOffset? LastCpuAt { get; set; }

    public bool? Reachable { get; set; }

    /// Why it isn't running, when that isn't obvious (gave up restarting, failed to start).
    public string? Detail { get; set; }
}
