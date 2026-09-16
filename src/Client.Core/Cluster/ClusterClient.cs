using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Cluster;

namespace FifthBox.ServerManager.Client.Core;

public interface IClusterClient
{
    Task<ClusterStatusResponse> GetStatusAsync(CancellationToken ct = default);
    Task<ClusterJoinResponse> GetJoinInfoAsync(CancellationToken ct = default);
    Task<ClusterStatusResponse> BootstrapAsync(CancellationToken ct = default);
}

public sealed class ClusterClient(HttpClient http) : IClusterClient
{
    public Task<ClusterStatusResponse> GetStatusAsync(CancellationToken ct = default) =>
        GetAsync<ClusterStatusResponse>("api/cluster", ct);

    public Task<ClusterJoinResponse> GetJoinInfoAsync(CancellationToken ct = default) =>
        GetAsync<ClusterJoinResponse>("api/cluster/join", ct);

    public async Task<ClusterStatusResponse> BootstrapAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/cluster/bootstrap", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<ClusterStatusResponse>(ct))!;
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }
}
