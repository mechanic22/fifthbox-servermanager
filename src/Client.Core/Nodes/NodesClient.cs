using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for the nodes API. Reads ProblemDetails on failure and throws
/// <see cref="ApiException"/> with a user-safe message. Transport-neutral — auth is applied by the
/// HttpClient pipeline (cookie on web, bearer on mobile), so it's shared by every head.
/// </summary>
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
