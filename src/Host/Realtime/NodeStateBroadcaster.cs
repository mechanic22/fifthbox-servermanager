using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Host.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Realtime;

/// Bridges the NodeStateChanged event onto the SignalR hub. Components never touch IHubContext — only
/// this Host-owned handler does, keeping the push path Eventing → Host hub → client.
public sealed class NodeStateBroadcaster(IHubContext<NodeHub> hub) : IEventHandler<NodeStateChanged>
{
    public Task HandleAsync(NodeStateChanged @event, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("NodeStateChanged", @event.Nodes, ct);
}
