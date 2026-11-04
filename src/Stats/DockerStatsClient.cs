using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Stats;

public interface IDockerStats
{
    Task<IReadOnlyList<SwarmContainer>> ListSwarmContainersAsync(CancellationToken ct);

    Task<DockerStatsSample> ReadStatsAsync(string containerId, CancellationToken ct);
}

public record SwarmContainer(string Id, string TaskId);

/// the socket is root on this node, so this only ever makes these two reads
/// no inspect on purpose, that's the call that hands back env vars
public sealed class DockerStatsClient(IOptions<StatsOptions> options) : IDockerStats, IDisposable
{
    private const string TaskLabel = "com.docker.swarm.task.id";

    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(options.Value.DockerSocket), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    })
    {
        BaseAddress = new Uri("http://docker/"),
    };

    public async Task<IReadOnlyList<SwarmContainer>> ListSwarmContainersAsync(CancellationToken ct)
    {
        var filter = Uri.EscapeDataString($$"""{"label":["{{TaskLabel}}"]}""");
        var containers = await _http.GetFromJsonAsync<List<ListedContainer>>($"containers/json?filters={filter}", ct);

        return (containers ?? [])
            .Where(c => c.Labels?.ContainsKey(TaskLabel) == true)
            .Select(c => new SwarmContainer(c.Id, c.Labels![TaskLabel]))
            .ToList();
    }

    // stream=false waits for two samples, one-shot would leave the cpu delta empty
    public async Task<DockerStatsSample> ReadStatsAsync(string containerId, CancellationToken ct) =>
        await _http.GetFromJsonAsync<DockerStatsSample>($"containers/{Uri.EscapeDataString(containerId)}/stats?stream=false", ct)
        ?? throw new InvalidOperationException("Docker returned no stats.");

    public void Dispose() => _http.Dispose();

    private sealed record ListedContainer(
        [property: JsonPropertyName("Id")] string Id,
        [property: JsonPropertyName("Labels")] Dictionary<string, string>? Labels);
}

public sealed record DockerStatsSample
{
    [JsonPropertyName("cpu_stats")] public CpuSample? Cpu { get; init; }
    [JsonPropertyName("precpu_stats")] public CpuSample? PreviousCpu { get; init; }
    [JsonPropertyName("memory_stats")] public MemorySample? Memory { get; init; }
}

public sealed record CpuSample
{
    [JsonPropertyName("cpu_usage")] public CpuUsage? Usage { get; init; }
    [JsonPropertyName("system_cpu_usage")] public ulong SystemUsage { get; init; }
    [JsonPropertyName("online_cpus")] public int OnlineCpus { get; init; }
}

public sealed record CpuUsage
{
    [JsonPropertyName("total_usage")] public ulong Total { get; init; }
    [JsonPropertyName("percpu_usage")] public IReadOnlyList<ulong>? PerCpu { get; init; }
}

public sealed record MemorySample
{
    [JsonPropertyName("usage")] public long Usage { get; init; }
    [JsonPropertyName("limit")] public long Limit { get; init; }
    [JsonPropertyName("stats")] public Dictionary<string, long>? Stats { get; init; }
}
