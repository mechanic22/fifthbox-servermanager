using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Client.Core;

public interface INodesClient
{
    Task<IReadOnlyList<NodeResponse>> ListAsync(CancellationToken ct = default);

    Task<NodeResponse> SetAvailabilityAsync(string id, NodeAvailability availability, CancellationToken ct = default);

    Task<NodeResponse> SetRoleAsync(string id, NodeRole role, CancellationToken ct = default);

    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed class NodesClient(HttpClient http) : INodesClient
{
    public async Task<IReadOnlyList<NodeResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/nodes", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<NodeResponse>>(ct) ?? [];
    }

    public Task<NodeResponse> SetAvailabilityAsync(string id, NodeAvailability availability, CancellationToken ct = default) =>
        PutAsync($"api/nodes/{id}/availability", new SetNodeAvailabilityRequest { Availability = availability }, ct);

    public Task<NodeResponse> SetRoleAsync(string id, NodeRole role, CancellationToken ct = default) =>
        PutAsync($"api/nodes/{id}/role", new SetNodeRoleRequest { Role = role }, ct);

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/nodes/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    private async Task<NodeResponse> PutAsync<TRequest>(string url, TRequest request, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync(url, request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<NodeResponse>(ct))!;
    }
}
