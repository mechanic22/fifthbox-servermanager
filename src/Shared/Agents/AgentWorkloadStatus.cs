namespace FifthBox.ServerManager.Shared.Agents;

/// An agent's report of a native workload's runtime state.
public record AgentWorkloadStatus
{
    public required string Name { get; init; }
    public bool Running { get; init; }
    public string? Detail { get; init; }

    public int? Pid { get; init; }
    public DateTimeOffset? StartedAt { get; init; }

    /// Automatic restarts in the current crash run — a run that stays up long enough clears it, as does
    /// an operator deploy. A climbing count is the signal that something is crash-looping now.
    public int RestartCount { get; init; }

    /// Exit code of the last run that ended on its own (null while it has never exited).
    public int? ExitCode { get; init; }

    /// The agent attached to an already-running process instead of starting a new one.
    public bool Adopted { get; init; }

    /// An acquire is in progress. Nothing will run until it finishes — the two are mutually exclusive
    /// because rewriting binaries under a live process corrupts the install.
    public bool Updating { get; init; }

    /// What the last successful acquire put on disk. Null when nothing has been acquired, including
    /// after an acquire that failed part-way.
    public string? InstalledVersion { get; init; }

    public long? MemoryBytes { get; init; }

    /// Share of one core since the last report, so 100 means one core saturated.
    public double? CpuPercent { get; init; }

    /// Whether something is actually accepting connections on the declared TCP ports. Null when there is
    /// nothing to probe — a UDP-only game server can't be checked this way, and reporting false for one
    /// would be worse than saying nothing.
    public bool? Reachable { get; init; }
}
