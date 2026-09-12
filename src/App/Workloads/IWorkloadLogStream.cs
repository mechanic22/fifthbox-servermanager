using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// A live tail of a container workload's output, open only while someone is watching. Yields a batch per
/// read rather than a line at a time — a burst of output is one message to the browser, not hundreds.
/// Native workloads have no equivalent here: their agent pushes.
public interface IWorkloadLogStream
{
    IAsyncEnumerable<IReadOnlyList<WorkloadLogLine>> FollowAsync(string serviceName, CancellationToken ct = default);
}
