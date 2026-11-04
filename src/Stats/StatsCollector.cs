using System.Collections.Concurrent;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Stats;

public sealed class StatsCollector(IDockerStats docker, ILogger<StatsCollector> log)
{
    // each read takes a second or two, one at a time would outlast the interval
    private const int MaxConcurrentReads = 8;

    public async Task<ContainerStatsReport> CollectAsync(long? hostMemoryBytes, CancellationToken ct)
    {
        var containers = await docker.ListSwarmContainersAsync(ct);
        var entries = new ConcurrentBag<ContainerStatsEntry>();

        await Parallel.ForEachAsync(
            containers,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentReads, CancellationToken = ct },
            async (container, token) =>
            {
                try
                {
                    var sample = await docker.ReadStatsAsync(container.Id, token);
                    entries.Add(StatsCalculator.ToEntry(container.TaskId, sample, hostMemoryBytes));
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    // containers come and go between the list and the read
                    log.LogDebug(ex, "Couldn't read stats for task {TaskId}", container.TaskId);
                }
            });

        return new ContainerStatsReport { Containers = [.. entries] };
    }
}
