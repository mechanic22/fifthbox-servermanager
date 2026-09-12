using FifthBox.ServerManager.App.Common;

namespace FifthBox.ServerManager.Host.Realtime;

/// The safety net under the docker event stream. Events are the signal; this catches what they can't see —
/// an agent's last-seen time, a replica that died on another node, anything missed while the socket was
/// down — and repairs it within a minute.
///
/// The whole sweep is three docker calls no matter how many workloads there are, which is what makes
/// running it unconditionally affordable.
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
