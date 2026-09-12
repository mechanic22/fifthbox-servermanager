using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace FifthBox.ServerManager.Client.Web.Services;

/// Owns the app's SignalR connections — one per hub, all in one place so no page or component ever
/// builds its own. Same-origin, so the auth cookie rides along and the client never handles a token.
/// Subscribers must unsubscribe on dispose or the handler outlives the page.
public sealed class RealtimeService(NavigationManager nav) : IAsyncDisposable
{
    private readonly Dictionary<string, HubConnection> _connections = [];

    public event Action<IReadOnlyList<NodeResponse>>? NodesChanged;

    /// A workload's runtime state changed — swarm workloads via the docker event stream, native ones from
    /// the agent's own supervision reports.
    public event Action<string, WorkloadRuntimeStatus>? WorkloadStatusChanged;

    /// New output for a workload whose Logs tab is open. Only flows between FollowLogsAsync and
    /// UnfollowLogsAsync — nothing streams when nobody is watching.
    public event Action<string, IReadOnlyList<WorkloadLogLine>>? WorkloadLogLines;

    /// A dropped connection came back. Every change during the gap was missed and no event will ever
    /// replay it, so subscribers re-read instead of waiting for the next one.
    public event Action? Reconnected;

    /// Raised when any connection drops or comes back, so the shell can say the status on screen has
    /// stopped updating. Without it a frozen chip keeps presenting its last value as current.
    public event Action? ConnectionStateChanged;

    /// True while any hub this app opened is not connected.
    public bool Disconnected => _connections.Values.Any(c => c.State != HubConnectionState.Connected);

    public Task EnsureNodesConnectedAsync() =>
        EnsureConnectedAsync("/hubs/nodes", c =>
            c.On<IReadOnlyList<NodeResponse>>("NodeStateChanged", nodes => NodesChanged?.Invoke(nodes)));

    public Task EnsureWorkloadsConnectedAsync() =>
        EnsureConnectedAsync("/hubs/workloads", c =>
        {
            c.On<string, WorkloadRuntimeStatus>("WorkloadStatusChanged", (id, status) => WorkloadStatusChanged?.Invoke(id, status));
            c.On<string, IReadOnlyList<WorkloadLogLine>>("WorkloadLogLines", (id, lines) => WorkloadLogLines?.Invoke(id, lines));
        });

    public async Task FollowLogsAsync(string workloadId)
    {
        await EnsureWorkloadsConnectedAsync();
        await _connections["/hubs/workloads"].InvokeAsync("FollowLogs", workloadId);
    }

    public async Task UnfollowLogsAsync(string workloadId)
    {
        if (_connections.TryGetValue("/hubs/workloads", out var c) && c.State == HubConnectionState.Connected)
        {
            await c.InvokeAsync("UnfollowLogs", workloadId);
        }
    }

    private async Task EnsureConnectedAsync(string path, Action<HubConnection> subscribe)
    {
        if (!_connections.TryGetValue(path, out var connection))
        {
            connection = new HubConnectionBuilder()
                .WithUrl(nav.ToAbsoluteUri(path))
                .WithAutomaticReconnect()
                .Build();

            subscribe(connection);
            connection.Reconnected += _ =>
            {
                Reconnected?.Invoke();
                ConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };
            connection.Reconnecting += _ =>
            {
                ConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };
            connection.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };
            _connections[path] = connection;
        }

        if (connection.State == HubConnectionState.Disconnected)
        {
            await connection.StartAsync();
            ConnectionStateChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
    }
}
