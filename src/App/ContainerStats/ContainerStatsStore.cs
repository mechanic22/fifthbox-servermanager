using System.Collections.Concurrent;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

public interface IContainerStatsStore
{
    void Record(IEnumerable<ContainerStatsEntry> entries);

    /// null when nothing recent came in, a node that stopped reporting shouldn't show its last numbers forever
    ContainerStatsEntry? Find(string taskId);
}

public sealed class ContainerStatsStore(TimeProvider clock) : IContainerStatsStore
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    // anyone holding the key could send made-up ids, so this can't grow forever
    public const int MaxEntries = 20_000;

    private readonly ConcurrentDictionary<string, (ContainerStatsEntry Entry, DateTimeOffset At)> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public void Record(IEnumerable<ContainerStatsEntry> entries)
    {
        var now = clock.GetUtcNow();
        foreach (var (taskId, value) in _entries)
        {
            if (now - value.At > Ttl)
            {
                _entries.TryRemove(taskId, out _);
            }
        }

        foreach (var entry in entries)
        {
            if (_entries.Count < MaxEntries || _entries.ContainsKey(entry.TaskId))
            {
                _entries[entry.TaskId] = (entry, now);
            }
        }
    }

    public ContainerStatsEntry? Find(string taskId) =>
        _entries.TryGetValue(taskId, out var value) && clock.GetUtcNow() - value.At <= Ttl ? value.Entry : null;
}
