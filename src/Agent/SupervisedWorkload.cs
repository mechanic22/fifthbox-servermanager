using System.Diagnostics;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

public sealed class SupervisedWorkload
{
    /// so the exit callback can't interleave with operator commands
    public Lock Gate { get; } = new();

    public required AgentWorkloadSpec Spec { get; set; }

    /// hash of the real spec. a restored spec has no env so it hashes differently
    public required string ConfigHash { get; set; }
    public Process? Process { get; set; }

    /// null for adopted processes, their pipes belong to whoever started them
    public StreamWriter? Input { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public int? LastExitCode { get; set; }

    /// set before signalling so the exit callback can tell a stop from a crash
    public bool StopRequested { get; set; }

    /// resets on deploy or once a run stays up long enough
    public int RestartCount { get; set; }

    public bool Adopted { get; set; }

    /// nothing starts while set, swapping binaries under a live process corrupts the install
    public bool Updating { get; set; }

    /// survives process restarts, not agent restarts. empty for adopted processes
    public LogBuffer Logs { get; } = new(LogCapacity);

    public const int LogCapacity = 500;

    public string? InstalledVersion { get; set; }

    public TimeSpan? LastCpu { get; set; }
    public DateTimeOffset? LastCpuAt { get; set; }

    public bool? Reachable { get; set; }

    public string? Detail { get; set; }
}
