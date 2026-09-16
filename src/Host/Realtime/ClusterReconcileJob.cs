using FifthBox.ServerManager.App.Common;

namespace FifthBox.ServerManager.Host.Realtime;

/// catches what docker events miss. only 3 docker calls however many workloads, so run it always
public sealed class ClusterReconcileJob(IClusterStateWriter writer) : IScheduledJob
{
    public string Name => "cluster";

    public TimeSpan Interval => TimeSpan.FromSeconds(60);

    public async Task RunAsync(CancellationToken ct = default)
    {
        await writer.RefreshNodesAsync(ct);
        await writer.RefreshWorkloadsAsync(ct);
    }
}
