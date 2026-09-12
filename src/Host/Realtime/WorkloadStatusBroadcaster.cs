using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Host.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Realtime;

public sealed class WorkloadStatusBroadcaster(IHubContext<WorkloadHub> hub, IWorkloadAudience audience)
    : IEventHandler<WorkloadStatusChanged>
{
    public async Task HandleAsync(WorkloadStatusChanged @event, CancellationToken ct = default)
    {
        // Addressed rather than broadcast: the payload names a workload, so sending it to everyone tells
        // every signed-in user what exists. SubjectUserIdProvider keys these on the sub claim.
        var viewers = await audience.ViewerIdsAsync(@event.WorkloadId, ct);
        if (viewers.Count == 0)
        {
            return;
        }

        await hub.Clients.Users(viewers).SendAsync("WorkloadStatusChanged", @event.WorkloadId, @event.Status, ct);
    }
}
