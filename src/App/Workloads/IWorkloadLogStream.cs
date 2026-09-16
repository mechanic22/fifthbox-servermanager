using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// only open while someone's watching, batches per read so a burst is one message
/// native workloads don't use this, their agent pushes
public interface IWorkloadLogStream
{
    IAsyncEnumerable<IReadOnlyList<WorkloadLogLine>> FollowAsync(string serviceName, CancellationToken ct = default);
}
