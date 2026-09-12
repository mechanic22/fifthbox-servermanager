namespace FifthBox.ServerManager.Shared.Agents;

/// What an agent reports about the machine it runs on. Disk is the one that matters most: the classic
/// small-PaaS outage is a full disk, and a game server install is tens of gigabytes.
///
/// Machine-wide CPU is deliberately absent — there is no cross-platform way to read it without
/// per-OS code, and it would be the least actionable number here.
public record AgentMetrics
{
    /// The drive holding the agent's workload root, which is where installs actually land.
    public long DiskTotalBytes { get; init; }
    public long DiskFreeBytes { get; init; }

    public int ProcessorCount { get; init; }

    /// Memory the agent process itself is holding — a supervisor that leaks is worth seeing.
    public long AgentMemoryBytes { get; init; }

    public DateTimeOffset ReportedAt { get; init; }
}
