namespace FifthBox.ServerManager.Shared.Workloads;

public static class ContainerStatsHeaders
{
    public const string Timestamp = "X-Fbsm-Timestamp";
    public const string Signature = "X-Fbsm-Signature";
}

public record ContainerStatsReport
{
    public IReadOnlyList<ContainerStatsEntry> Containers { get; init; } = [];
    public MachineStats? Machine { get; init; }
}

public record ContainerStatsEntry
{
    /// swarm's task id, how a container lines up with a replica
    public required string TaskId { get; init; }

    /// docker stats convention, 100 is one core
    public double CpuPercent { get; init; }
    public long MemoryBytes { get; init; }

    /// null when the container has no limit
    public long? MemoryLimitBytes { get; init; }
}
