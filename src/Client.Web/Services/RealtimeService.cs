using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace FifthBox.ServerManager.Client.Web.Services;

public sealed class RealtimeService(NavigationManager nav) : IAsyncDisposable
{
    private readonly Dictionary<string, HubConnection> _connections = [];

    public event Action<IReadOnlyList<NodeResponse>>? NodesChanged;

    public event Action<string, WorkloadRuntimeStatus>? WorkloadStatusChanged;

    /// only fires between FollowLogsAsync and UnfollowLogsAsync
    public event Action<string, IReadOnlyList<WorkloadLogLine>>? WorkloadLogLines;

    /// anything during the gap is gone and won't replay, so re-read on this
    public event Action? Reconnected;

    public event Action? ConnectionStateChanged;

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
