namespace FifthBox.ServerManager.Shared.Agents;

public record AgentWorkloadStatus
{
    public required string Name { get; init; }
    public bool Running { get; init; }
    public string? Detail { get; init; }

    public int? Pid { get; init; }
    public DateTimeOffset? StartedAt { get; init; }

    /// auto restarts in the current crash run, reset by staying up or a deploy. climbing = crash loop
    public int RestartCount { get; init; }

    /// last run that ended on its own, null if it never has
    public int? ExitCode { get; init; }

    /// attached to a process that was already running instead of starting one
    public bool Adopted { get; init; }

    /// acquire in progress, nothing runs until it's done or we'd rewrite binaries under a live process
    public bool Updating { get; init; }

    /// from the last good acquire, null after a half-failed one too
    public string? InstalledVersion { get; init; }

    public long? MemoryBytes { get; init; }

    /// share of one core since the last report, 100 = one core pegged
    public double? CpuPercent { get; init; }

    /// something's listening on the declared tcp ports. null when there's nothing to probe (udp only)
    public bool? Reachable { get; init; }
}
