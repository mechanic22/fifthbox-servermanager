using System.IO.Pipes;
using System.Net.Sockets;

namespace FifthBox.ServerManager.Integrations.Swarm;

public interface ISwarmTaskLogs
{
    Task<(string Stdout, string Stderr)> ReadAsync(string taskId, bool tty, int tail, CancellationToken ct);
}

/// docker's one-task logs, Docker.DotNet only wraps the whole-service call
public sealed class SwarmTaskLogs(Uri dockerEndpoint) : ISwarmTaskLogs, IDisposable
{
    private const int MaxBytes = 16 * 1024 * 1024;

    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = (_, ct) => ConnectAsync(dockerEndpoint, ct),
    })
    {
        BaseAddress = new Uri("http://docker/"),
        Timeout = Timeout.InfiniteTimeSpan,
    };

    public async Task<(string Stdout, string Stderr)> ReadAsync(string taskId, bool tty, int tail, CancellationToken ct)
    {
        using var response = await _http.GetAsync(
            $"tasks/{Uri.EscapeDataString(taskId)}/logs?stdout=1&stderr=1&timestamps=1&tail={tail}",
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await DockerLogFrames.ReadAsync(stream, tty, MaxBytes, ct);
    }

    public void Dispose() => _http.Dispose();

    private static async ValueTask<Stream> ConnectAsync(Uri endpoint, CancellationToken ct)
    {
        if (endpoint.Scheme == "npipe")
        {
            var pipe = new NamedPipeClientStream(".", endpoint.Segments[^1], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ct);
            return pipe;
        }

        var socket = endpoint.Scheme == "unix"
            ? new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            : new Socket(SocketType.Stream, ProtocolType.Tcp);

        try
        {
            if (endpoint.Scheme == "unix")
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.LocalPath), ct);
            }
            else
            {
                await socket.ConnectAsync(endpoint.Host, endpoint.Port, ct);
            }

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
