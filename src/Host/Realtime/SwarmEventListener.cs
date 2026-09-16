using FifthBox.ServerManager.App.Cluster;

namespace FifthBox.ServerManager.Host.Realtime;

/// events only say what moved, so we re-read. dupes cost a read, misses get caught by reconcile
public sealed class SwarmEventListener(
    ISwarmEvents events,
    IClusterStateWriter writer,
    ILogger<SwarmEventListener> logger) : BackgroundService
{
    // one deploy fires a burst of events per replica, batch them into one refresh
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? lastFailure = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // stream missed whatever happened while it was down, so re-read first
                await writer.RefreshNodesAsync(stoppingToken);
                await writer.RefreshWorkloadsAsync(stoppingToken);
                await ConsumeAsync(stoppingToken);
                lastFailure = null;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // no daemon fails this forever, only the first one gets a stack trace
                if (lastFailure == ex.Message)
                {
                    logger.LogDebug("Docker event stream still failing: {Reason}", ex.Message);
                }
                else
                {
                    lastFailure = ex.Message;
                    logger.LogError(ex, "Docker event stream dropped; reconnecting");
                }
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        await using var stream = events.WatchAsync(ct).GetAsyncEnumerator(ct);
        var pendingNodes = false;
        var pendingServices = new HashSet<string>(StringComparer.Ordinal);

        // held across loops, a timed-out wait has to resume the same read
        var next = stream.MoveNextAsync().AsTask();

        while (true)
        {
            if (pendingNodes || pendingServices.Count > 0)
            {
                try
                {
                    if (!await next.WaitAsync(CoalesceWindow, ct))
                    {
                        break;
                    }
                }
                catch (TimeoutException)
                {
                    await FlushAsync(pendingNodes, pendingServices, ct);
                    pendingNodes = false;
                    pendingServices.Clear();
                    continue;
                }
            }
            else if (!await next)
            {
                break;
            }

            var change = stream.Current;
            if (change.Kind == SwarmChangeKind.Node)
            {
                pendingNodes = true;
            }
            else if (change.ServiceName is { } service)
            {
                pendingServices.Add(service);
            }

            next = stream.MoveNextAsync().AsTask();
        }

        await FlushAsync(pendingNodes, pendingServices, ct);
    }

    private async Task FlushAsync(bool nodes, HashSet<string> services, CancellationToken ct)
    {
        if (nodes)
        {
            await writer.RefreshNodesAsync(ct);
        }

        // sweep is 2 docker calls total, per-service is 3 each
        if (services.Count > 1)
        {
            await writer.RefreshWorkloadsAsync(ct);
            return;
        }

        foreach (var service in services)
        {
            await writer.RefreshWorkloadAsync(service, ct);
        }
    }
}
