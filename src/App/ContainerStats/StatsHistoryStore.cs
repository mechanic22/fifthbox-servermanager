using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

public interface IStatsHistoryStore
{
    void RecordReplica(string workloadId, WorkloadTask task, ContainerStatsEntry entry);

    void RecordMachine(MachineStats stats);

    IReadOnlyList<ReplicaStatsHistory> ForWorkload(string workloadId, StatsHistoryRange range);

    IReadOnlyList<MachineStatsHistory> Machines(StatsHistoryRange range);
}

/// memory only, a Host restart starts the charts again
public sealed class StatsHistoryStore(TimeProvider clock) : IStatsHistoryStore
{
    // a series is about 40 KB when full, so these bound it at about 80 MB
    public const int MaxReplicaSeries = 2_000;
    public const int MaxMachineSeries = 200;

    private readonly Lock _gate = new();
    private readonly Dictionary<(string WorkloadId, string Key), ReplicaSeries> _replicas = [];
    private readonly Dictionary<string, MachineSeries> _machines = new(StringComparer.Ordinal);
    private long _lastPrune;

    /// a global service's tasks are all slot 0, the node is what tells them apart
    public static string KeyFor(WorkloadTask task) => task.Slot > 0 ? $"slot-{task.Slot}" : $"node-{task.NodeId}";

    public void RecordReplica(string workloadId, WorkloadTask task, ContainerStatsEntry entry)
    {
        var now = clock.GetUtcNow();
        var key = (workloadId, KeyFor(task));

        lock (_gate)
        {
            Prune(now);
            if (!_replicas.TryGetValue(key, out var replica))
            {
                if (_replicas.Count >= MaxReplicaSeries)
                {
                    return;
                }

                replica = new ReplicaSeries(task.Slot, task.NodeId);
                _replicas[key] = replica;
            }

            replica.Series.Add(now, entry.CpuPercent, entry.MemoryBytes);
        }
    }

    public void RecordMachine(MachineStats stats)
    {
        var now = clock.GetUtcNow();

        lock (_gate)
        {
            Prune(now);
            if (!_machines.TryGetValue(stats.NodeId, out var machine))
            {
                if (_machines.Count >= MaxMachineSeries)
                {
                    return;
                }

                machine = new MachineSeries();
                _machines[stats.NodeId] = machine;
            }

            machine.MemoryTotalBytes = stats.MemoryTotalBytes;
            machine.CpuCores = stats.CpuCores;
            machine.DiskFreeBytes = stats.DiskFreeBytes;

            // the first read has no cpu delta, a zero there would be a false dip
            if (stats.CpuPercent is { } cpu)
            {
                machine.Series.Add(now, cpu, stats.MemoryUsedBytes);
            }
        }
    }

    public IReadOnlyList<ReplicaStatsHistory> ForWorkload(string workloadId, StatsHistoryRange range)
    {
        lock (_gate)
        {
            return _replicas
                .Where(r => r.Key.WorkloadId == workloadId)
                .OrderBy(r => r.Value.Slot)
                .ThenBy(r => r.Key.Key, StringComparer.Ordinal)
                .Select(r => new ReplicaStatsHistory
                {
                    Key = r.Key.Key,
                    Slot = r.Value.Slot,
                    NodeId = r.Value.NodeId,
                    Points = r.Value.Series.Read(range),
                })
                .ToList();
        }
    }

    public IReadOnlyList<MachineStatsHistory> Machines(StatsHistoryRange range)
    {
        lock (_gate)
        {
            return _machines
                .Select(m => new MachineStatsHistory
                {
                    NodeId = m.Key,
                    MemoryTotalBytes = m.Value.MemoryTotalBytes,
                    CpuCores = m.Value.CpuCores,
                    DiskFreeBytes = m.Value.DiskFreeBytes,
                    Points = m.Value.Series.Read(range),
                })
                .ToList();
        }
    }

    // scaled-down or deleted workloads stop reporting, their lines go once they've aged out
    private void Prune(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        if (seconds - _lastPrune < 60)
        {
            return;
        }

        _lastPrune = seconds;
        var cutoff = seconds - (long)StatsSeries.FullWindow.TotalSeconds;

        foreach (var key in _replicas.Where(r => r.Value.Series.LastAt < cutoff).Select(r => r.Key).ToList())
        {
            _replicas.Remove(key);
        }

        foreach (var key in _machines.Where(m => m.Value.Series.LastAt < cutoff).Select(m => m.Key).ToList())
        {
            _machines.Remove(key);
        }
    }

    private sealed record ReplicaSeries(int Slot, string? NodeId)
    {
        public StatsSeries Series { get; } = new();
    }

    private sealed class MachineSeries
    {
        public StatsSeries Series { get; } = new();
        public long MemoryTotalBytes { get; set; }
        public int? CpuCores { get; set; }
        public long? DiskFreeBytes { get; set; }
    }
}
