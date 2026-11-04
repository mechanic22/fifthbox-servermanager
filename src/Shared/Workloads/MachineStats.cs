namespace FifthBox.ServerManager.Shared.Workloads;

public record MachineStats
{
    /// swarm's node id, how it lines up with a machine row
    public required string NodeId { get; init; }

    /// the whole machine, 100 is every core. null on the first read, it takes two to get a delta
    public double? CpuPercent { get; init; }

    /// null from a worker older than this field
    public int? CpuCores { get; init; }

    public long MemoryUsedBytes { get; init; }
    public long MemoryTotalBytes { get; init; }

    /// the drive docker keeps its data on, null if it couldn't be read
    public long? DiskFreeBytes { get; init; }
    public long? DiskTotalBytes { get; init; }
}
