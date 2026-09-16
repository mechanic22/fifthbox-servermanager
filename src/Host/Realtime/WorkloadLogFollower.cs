using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Host.Hubs;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Realtime;

public interface IWorkloadLogFollower
{
    Task FollowAsync(string connectionId, string workloadId, CancellationToken ct = default);
    Task UnfollowAsync(string connectionId, string workloadId, CancellationToken ct = default);

    Task ReleaseAllAsync(string connectionId, CancellationToken ct = default);

    bool IsFollowed(string workloadId);

    Task PublishAsync(string workloadId, IReadOnlyList<WorkloadLogLine> lines, CancellationToken ct = default);
}

/// ref-counted so nothing streams while nobody's looking
public sealed class WorkloadLogFollower(
    IHubContext<WorkloadHub> hub,
    IServiceScopeFactory scopeFactory,
    ILogger<WorkloadLogFollower> logger) : IWorkloadLogFollower, IAsyncDisposable
{
    private static readonly TimeSpan ReopenDelay = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, HashSet<string>> _watchers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _swarmStreams = new();
    private readonly Lock _gate = new();

    public bool IsFollowed(string workloadId) => _watchers.ContainsKey(workloadId);

    public static string GroupName(string workloadId) => $"logs:{workloadId}";

    public async Task FollowAsync(string connectionId, string workloadId, CancellationToken ct = default)
    {
        bool first;
        lock (_gate)
        {
            var set = _watchers.GetOrAdd(workloadId, _ => []);
            first = set.Count == 0;
            set.Add(connectionId);
        }

        await hub.Groups.AddToGroupAsync(connectionId, GroupName(workloadId), ct);

        if (first)
        {
            await StartSourceAsync(workloadId, ct);
        }
    }

    public async Task UnfollowAsync(string connectionId, string workloadId, CancellationToken ct = default)
    {
        var last = false;
        lock (_gate)
        {
            if (_watchers.TryGetValue(workloadId, out var set) && set.Remove(connectionId) && set.Count == 0)
            {
                _watchers.TryRemove(workloadId, out _);
                last = true;
            }
        }

        await hub.Groups.RemoveFromGroupAsync(connectionId, GroupName(workloadId), ct);

        if (last)
        {
            await StopSourceAsync(workloadId, ct);
        }
    }

    public async Task ReleaseAllAsync(string connectionId, CancellationToken ct = default)
    {
        string[] watched;
        lock (_gate)
        {
            watched = [.. _watchers.Where(kv => kv.Value.Contains(connectionId)).Select(kv => kv.Key)];
        }

        foreach (var workloadId in watched)
        {
            await UnfollowAsync(connectionId, workloadId, ct);
        }
    }

    public Task PublishAsync(string workloadId, IReadOnlyList<WorkloadLogLine> lines, CancellationToken ct = default)
        => lines.Count == 0
            ? Task.CompletedTask
            : hub.Clients.Group(GroupName(workloadId)).SendAsync("WorkloadLogLines", workloadId, lines, ct);

    private async Task StartSourceAsync(string workloadId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workloads = scope.ServiceProvider.GetRequiredService<IWorkloadRepository>();
            var workload = await workloads.FindByIdAsync(workloadId, ct);
            if (workload is null)
            {
                return;
            }

            if (workload.Kind == WorkloadKind.Native && workload.AgentId is not null)
            {
                var channel = scope.ServiceProvider.GetRequiredService<IAgentCommandChannel>();
                await channel.StartFollowingLogsAsync(workload.AgentId, workload.Name, ct);
            }
            else if (workload.Kind == WorkloadKind.Container)
            {
                StartSwarmStreaming(workloadId, SwarmNaming.ServiceName(workload.Name));
            }
        }
        catch (Exception ex)
        {
            // best effort, the tab still has its tail and Refresh
            logger.LogWarning(ex, "Could not start following logs for {WorkloadId}", workloadId);
        }
    }

    private async Task StopSourceAsync(string workloadId, CancellationToken ct)
    {
        if (_swarmStreams.TryRemove(workloadId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workloads = scope.ServiceProvider.GetRequiredService<IWorkloadRepository>();
            var workload = await workloads.FindByIdAsync(workloadId, ct);
            if (workload is { Kind: WorkloadKind.Native, AgentId: not null })
            {
                var channel = scope.ServiceProvider.GetRequiredService<IAgentCommandChannel>();
                await channel.StopFollowingLogsAsync(workload.AgentId, workload.Name, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not stop following logs for {WorkloadId}", workloadId);
        }
    }

    private void StartSwarmStreaming(string workloadId, string serviceName)
    {
        var cts = new CancellationTokenSource();
        if (!_swarmStreams.TryAdd(workloadId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            var token = cts.Token;

            try
            {
                // stream ends with the container, reopen so an open tab survives redeploys and crashes
                while (!token.IsCancellationRequested)
                {
                    await using (var scope = scopeFactory.CreateAsyncScope())
                    {
                        var logs = scope.ServiceProvider.GetRequiredService<IWorkloadLogStream>();

                        await foreach (var lines in logs.FollowAsync(serviceName, token))
                        {
                            await PublishAsync(workloadId, lines, token);
                        }
                    }

                    // also throttles a missing service, which ends the stream straight away
                    await Task.Delay(ReopenDelay, token);
                }
            }
            catch (OperationCanceledException)
            {
                // last watcher left
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Swarm log stream stopped for {WorkloadId}", workloadId);
            }
            finally
            {
                // a leftover entry makes TryAdd fail forever
                if (_swarmStreams.TryRemove(new KeyValuePair<string, CancellationTokenSource>(workloadId, cts)))
                {
                    cts.Dispose();
                }
            }
        }, cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cts in _swarmStreams.Values)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        _swarmStreams.Clear();
    }
}
