namespace FifthBox.ServerManager.Shared.Workloads;

public enum StatsHistoryRange
{
    ThirtyMinutes,
    Day,
}

/// <param name="At">unix seconds</param>
public record StatsPoint(long At, double Cpu, long Memory);

public record ReplicaStatsHistory
{
    /// survives a restart, a replacement task is the same line
    public required string Key { get; init; }
    public int Slot { get; init; }
    public string? NodeId { get; init; }
    public IReadOnlyList<StatsPoint> Points { get; init; } = [];
}

public record MachineStatsHistory
{
    public required string NodeId { get; init; }
    public long MemoryTotalBytes { get; init; }

    /// null from a worker older than this field
    public int? CpuCores { get; init; }

    /// what the last report said, there's no history behind it
    public long? DiskFreeBytes { get; init; }

    public IReadOnlyList<StatsPoint> Points { get; init; } = [];
}
