using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// two api calls no matter how many workloads, cheap enough to always sweep
/// also the only way to see a replica die on another node, container events stay on their daemon
public interface IWorkloadStatusSource
{
    Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetAllAsync(CancellationToken ct = default);
}
