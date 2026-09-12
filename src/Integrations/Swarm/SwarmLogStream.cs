using System.Runtime.CompilerServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmLogStream(IDockerClient client) : IWorkloadLogStream
{
    private const int ReadBufferBytes = 16 * 1024;

    public async IAsyncEnumerable<IReadOnlyList<WorkloadLogLine>> FollowAsync(
        string serviceName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var services = await client.Swarm.ListServicesAsync(cancellationToken: ct);
        var service = services.FirstOrDefault(s => string.Equals(s.Spec?.Name, serviceName, StringComparison.Ordinal));
        if (service is null)
        {
            yield break;
        }

        // Whether the payload carries the 8-byte stdout/stderr framing depends on how the service was
        // created, so ask rather than assume — guessing wrong throws "unknown stream type".
        var tty = service.Spec?.TaskTemplate?.ContainerSpec?.TTY ?? false;

        using var stream = await client.Swarm.GetServiceLogsAsync(service.ID, tty, new ServiceLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Follow = true,
            // The page fetched its history over HTTP before subscribing, so this carries only what's new.
            Tail = "0",
        }, ct);

        var stdout = new SwarmLogBuffer();
        var stderr = new SwarmLogBuffer();
        var bytes = new byte[ReadBufferBytes];

        while (!ct.IsCancellationRequested)
        {
            var read = await stream.ReadOutputAsync(bytes, 0, bytes.Length, ct);
            if (read.EOF)
            {
                break;
            }

            var target = read.Target == MultiplexedStream.TargetStream.StandardError
                ? LogStream.Stderr
                : LogStream.Stdout;

            var lines = (target == LogStream.Stderr ? stderr : stdout).Feed(bytes, read.Count, target);
            if (lines.Count > 0)
            {
                yield return lines;
            }
        }
    }
}
