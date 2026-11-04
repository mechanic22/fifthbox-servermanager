using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

public static class ContainerStatsRouting
{
    public static IEnumerable<(string WorkloadId, WorkloadTask Task, ContainerStatsEntry Entry)> Matches(
        IReadOnlyDictionary<string, WorkloadRuntimeStatus> statuses,
        IReadOnlyList<ContainerStatsEntry> entries)
    {
        var byTask = entries.ToDictionary(e => e.TaskId, StringComparer.Ordinal);

        foreach (var (workloadId, status) in statuses)
        {
            foreach (var task in status.Tasks)
            {
                if (byTask.TryGetValue(task.Id, out var entry))
                {
                    yield return (workloadId, task, entry);
                }
            }
        }
    }

    /// only ids swarm says belong to a workload, a made-up id goes nowhere
    public static Dictionary<string, List<ContainerStatsEntry>> ByWorkload(
        IReadOnlyDictionary<string, WorkloadRuntimeStatus> statuses,
        IReadOnlyList<ContainerStatsEntry> entries)
    {
        return Matches(statuses, entries)
            .GroupBy(m => m.WorkloadId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Entry).ToList(), StringComparer.Ordinal);
    }
}
