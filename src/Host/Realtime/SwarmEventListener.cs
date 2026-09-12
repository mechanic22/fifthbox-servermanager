using FifthBox.ServerManager.App.Cluster;

namespace FifthBox.ServerManager.Host.Realtime;

/// Watches the docker event stream and turns what it sees into state refreshes. Events say only *which*
/// subject moved, never what it moved to — the refresh re-reads the truth, so a duplicate event costs a
/// wasted read and a missed one is caught by the reconcile.
public sealed class SwarmEventListener(
    ISwarmEvents events,
    IClusterStateWriter writer,
    ILogger<SwarmEventListener> logger) : BackgroundService
{
    // A single deploy fires create + start + health_status across every replica. Batching them means one
    // refresh and one broadcast instead of a burst of each.
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? lastFailure = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Whatever happened while the stream was down is invisible to it, so start by re-reading
                // rather than waiting for the next event to reveal we're stale.
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
                // A daemon that isn't there fails this every few seconds forever; only the first of a
                // repeating failure is worth a stack trace.
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

        // Held across iterations: a timed-out wait must resume the *same* pending read, not start another.
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

        // One service costs less to read on its own; past that the sweep is two docker calls for the
        // lot, where the per-service path is three each.
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
