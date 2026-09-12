using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// Every deployed container workload's runtime status in one pass, keyed by docker service name.
///
/// Two API calls regardless of how many workloads exist, which is what makes a periodic sweep cheap
/// enough to run whether or not anyone is watching. That sweep is also the only way the Host learns a
/// replica died on a *different* node: container events never leave the daemon they happened on, and
/// docker has no swarm-scope event for a task changing state.
public interface IWorkloadStatusSource
{
    Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetAllAsync(CancellationToken ct = default);
}
