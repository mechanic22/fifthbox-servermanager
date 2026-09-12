using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Cluster;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmEventSource(IDockerClient client) : ISwarmEvents
{
    // Health checks fire exec_create/exec_start/exec_die per container per interval — measured at 97% of
    // all traffic with four services on a 10s probe. Filtering at the daemon rather than after
    // deserialising is the difference between a quiet stream and a permanent hot loop.
    private static readonly IDictionary<string, IDictionary<string, bool>> Filters =
        new Dictionary<string, IDictionary<string, bool>>
        {
            ["type"] = Set("service", "node", "container"),
            ["event"] = Set("create", "update", "remove", "start", "die", "destroy", "kill", "health_status"),
        };

    public async IAsyncEnumerable<SwarmChange> WatchAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<SwarmChange>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        var monitor = client.System.MonitorEventsAsync(
            new ContainerEventsParameters { Filters = Filters },
            new ChannelProgress(channel.Writer),
            ct);

        // Whatever ends the docker call — daemon restart, cancellation, a socket that went away — has to
        // end the enumeration too, or the reader waits on a channel nobody will ever write to again.
        _ = monitor.ContinueWith(
            t => channel.Writer.TryComplete(t.Exception),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        await foreach (var change in channel.Reader.ReadAllAsync(ct))
        {
            yield return change;
        }

        await monitor;
    }

    private static IDictionary<string, bool> Set(params string[] values)
        => values.ToDictionary(v => v, _ => true);

    /// Progress<T> posts through the synchronization context, which reorders reports and would let a
    /// "removed" overtake the "created" before it. This one hands straight to the channel.
    private sealed class ChannelProgress(ChannelWriter<SwarmChange> writer) : IProgress<Message>
    {
        public void Report(Message value)
        {
            if (SwarmEventInterpreter.Interpret(value) is { } change)
            {
                writer.TryWrite(change);
            }
        }
    }
}
