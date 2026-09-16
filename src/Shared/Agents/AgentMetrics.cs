namespace FifthBox.ServerManager.Shared.Agents;

/// no machine-wide cpu on purpose, there's no cross-platform way to read it
public record AgentMetrics
{
    /// the drive holding the workload root, where installs land
    public long DiskTotalBytes { get; init; }
    public long DiskFreeBytes { get; init; }

    public int ProcessorCount { get; init; }

    /// just the agent process, so a leaky supervisor shows up
    public long AgentMemoryBytes { get; init; }

    public DateTimeOffset ReportedAt { get; init; }
}
