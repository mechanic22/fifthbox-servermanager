using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

public interface IWorkloadStatsReader
{
    Task<IReadOnlyList<ContainerStatsEntry>> ListAsync(Caller caller, string workloadId, CancellationToken ct = default);

    Task<IReadOnlyList<ReplicaStatsHistory>> HistoryAsync(Caller caller, string workloadId, StatsHistoryRange range, CancellationToken ct = default);
}

public sealed class WorkloadStatsReader(WorkloadLoader loader, IClusterState state, IContainerStatsStore store, IStatsHistoryStore history) : IWorkloadStatsReader
{
    public async Task<IReadOnlyList<ReplicaStatsHistory>> HistoryAsync(Caller caller, string workloadId, StatsHistoryRange range, CancellationToken ct = default)
    {
        await loader.LoadAsync(caller, workloadId, AccessLevel.View, ct);
        return history.ForWorkload(workloadId, range);
    }

    public async Task<IReadOnlyList<ContainerStatsEntry>> ListAsync(Caller caller, string workloadId, CancellationToken ct = default)
    {
        await loader.LoadAsync(caller, workloadId, AccessLevel.View, ct);

        return (state.StatusFor(workloadId)?.Tasks ?? [])
            .Select(t => store.Find(t.Id))
            .OfType<ContainerStatsEntry>()
            .ToList();
    }
}
