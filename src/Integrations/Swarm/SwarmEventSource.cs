using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Cluster;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmEventSource(IDockerClient client) : ISwarmEvents
{
    // health checks are ~97% of events (exec_*), filter at the daemon or it's a hot loop
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

        // whatever ends the docker call has to end the enumeration or the reader waits forever
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

    /// not Progress<T>, that posts through the sync context and can put "removed" before "created"
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
