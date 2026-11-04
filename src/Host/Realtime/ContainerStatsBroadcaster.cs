using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Host.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Realtime;

public sealed class ContainerStatsBroadcaster(
    IHubContext<WorkloadHub> workloads,
    IHubContext<NodeHub> nodes,
    IClusterState state,
    IWorkloadAudience audience) : IEventHandler<ContainerStatsReceived>
{
    public async Task HandleAsync(ContainerStatsReceived @event, CancellationToken ct = default)
    {
        if (@event.Report.Machine is { } machine)
        {
            await nodes.Clients.All.SendAsync("MachineStatsChanged", machine, ct);
        }

        foreach (var (workloadId, entries) in ContainerStatsRouting.ByWorkload(state.Statuses, @event.Report.Containers))
        {
            var viewers = await audience.ViewerIdsAsync(workloadId, ct);
            if (viewers.Count > 0)
            {
                await workloads.Clients.Users(viewers).SendAsync("WorkloadStatsChanged", workloadId, entries, ct);
            }
        }
    }
}
