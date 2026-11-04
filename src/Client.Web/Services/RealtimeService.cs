using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace FifthBox.ServerManager.Client.Web.Services;

public sealed class RealtimeService(NavigationManager nav, ILogger<RealtimeService> log) : IAsyncDisposable
{
    private readonly Dictionary<string, HubConnection> _connections = [];
    private readonly Dictionary<string, Task> _starts = [];
    private readonly HashSet<string> _followedLogs = [];

    public event Action<IReadOnlyList<NodeResponse>>? NodesChanged;

    public event Action<MachineStats>? MachineStatsChanged;

    public event Action<string, WorkloadRuntimeStatus>? WorkloadStatusChanged;

    public event Action<string, IReadOnlyList<ContainerStatsEntry>>? WorkloadStatsChanged;

    /// only fires between FollowLogsAsync and UnfollowLogsAsync
    public event Action<string, IReadOnlyList<WorkloadLogLine>>? WorkloadLogLines;

    /// anything during the gap is gone and won't replay, so re-read on this
    public event Action? Reconnected;

    public event Action? ConnectionStateChanged;

    public bool Disconnected => _connections.Values.Any(c => c.State != HubConnectionState.Connected);

    /// false while Disconnected means it gave up, nothing retries until a page calls Ensure again
    public bool Reconnecting => _connections.Values.Any(c => c.State is HubConnectionState.Reconnecting or HubConnectionState.Connecting);

    public Task EnsureNodesConnectedAsync() =>
        EnsureConnectedAsync("/hubs/nodes", c =>
        {
            c.On<IReadOnlyList<NodeResponse>>("NodeStateChanged", nodes => NodesChanged?.Invoke(nodes));
            c.On<MachineStats>("MachineStatsChanged", stats => MachineStatsChanged?.Invoke(stats));
        });

    public Task EnsureWorkloadsConnectedAsync() =>
        EnsureConnectedAsync("/hubs/workloads", c =>
        {
            c.On<string, WorkloadRuntimeStatus>("WorkloadStatusChanged", (id, status) => WorkloadStatusChanged?.Invoke(id, status));
            c.On<string, IReadOnlyList<ContainerStatsEntry>>("WorkloadStatsChanged", (id, stats) => WorkloadStatsChanged?.Invoke(id, stats));
            c.On<string, IReadOnlyList<WorkloadLogLine>>("WorkloadLogLines", (id, lines) => WorkloadLogLines?.Invoke(id, lines));
            c.Reconnected += _ => RefollowLogsAsync(c);
        });

    public async Task FollowLogsAsync(string workloadId)
    {
        _followedLogs.Add(workloadId);
        await EnsureWorkloadsConnectedAsync();
        await _connections["/hubs/workloads"].InvokeAsync("FollowLogs", workloadId);
    }

    public async Task UnfollowLogsAsync(string workloadId)
    {
        _followedLogs.Remove(workloadId);
        if (_connections.TryGetValue("/hubs/workloads", out var c) && c.State == HubConnectionState.Connected)
        {
            await c.InvokeAsync("UnfollowLogs", workloadId);
        }
    }

    // a reconnect is a new connection id, the hub has forgotten who was following
    private async Task RefollowLogsAsync(HubConnection connection)
    {
        foreach (var workloadId in _followedLogs.ToList())
        {
            try
            {
                await connection.InvokeAsync("FollowLogs", workloadId);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Couldn't re-follow logs for {WorkloadId}", workloadId);
            }
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

        _starts.TryGetValue(path, out var start);
        if (connection.State == HubConnectionState.Disconnected && start is not { IsCompleted: false })
        {
            start = StartAsync(path, connection);
            _starts[path] = start;
        }

        if (start is not null)
        {
            await start;
        }
    }

    // pages still work on fetched data without the socket, the next page's Ensure tries again
    private async Task StartAsync(string path, HubConnection connection)
    {
        try
        {
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Couldn't connect to {Hub}", path);
        }

        ConnectionStateChanged?.Invoke();
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
