using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Host.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// Server→client push of workload runtime state and, while a Logs tab is open, its output. Status
/// updates originate from agents reporting to AgentHub; log lines only flow for followed workloads.
[Authorize]
public sealed class WorkloadHub(IWorkloadLogFollower follower, IWorkloadAudience audience) : Hub
{
    public async Task FollowLogs(string workloadId)
    {
        await audience.RequireViewAsync(Context.User!.ToCaller(), workloadId);
        await follower.FollowAsync(Context.ConnectionId, workloadId);
    }

    public Task UnfollowLogs(string workloadId) => follower.UnfollowAsync(Context.ConnectionId, workloadId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // A closed tab never says goodbye, so the source would stream forever without this.
        await follower.ReleaseAllAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
