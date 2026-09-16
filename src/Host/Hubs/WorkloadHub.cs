using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Host.Infrastructure;
using FifthBox.ServerManager.Host.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

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
        // closed tabs never unfollow, so this is the only cleanup
        await follower.ReleaseAllAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
