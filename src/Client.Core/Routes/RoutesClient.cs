using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core;

public interface IRoutesClient
{
    Task<IReadOnlyList<RouteResponse>> ListAsync(CancellationToken ct = default);
    Task<RouteResponse> CreateAsync(CreateRouteRequest request, CancellationToken ct = default);
    Task<RouteResponse> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<RouteResponse> SetEnabledAsync(string id, bool enabled, CancellationToken ct = default);
    Task<string> GetNginxConfigAsync(CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> ApplyAsync(CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> GetProxyStatusAsync(CancellationToken ct = default);

    /// edge status plus whether saved routes and certs have reached it
    Task<ProxyStateResponse> GetProxyStateAsync(CancellationToken ct = default);
}

public sealed class RoutesClient(HttpClient http) : IRoutesClient
{
    public async Task<IReadOnlyList<RouteResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/routes", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<RouteResponse>>(ct) ?? [];
    }

    public async Task<RouteResponse> CreateAsync(CreateRouteRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/routes", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RouteResponse>(ct))!;
    }

    public async Task<RouteResponse> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/routes/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RouteResponse>(ct))!;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/routes/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task<RouteResponse> SetEnabledAsync(string id, bool enabled, CancellationToken ct = default)
    {
        using var response = await http.PatchAsJsonAsync($"api/routes/{id}/enabled", new SetRouteEnabledRequest(enabled), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RouteResponse>(ct))!;
    }

    public async Task<string> GetNginxConfigAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/routes/nginx-config", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<WorkloadRuntimeStatus> ApplyAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/routes/apply", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadRuntimeStatus>(ct))!;
    }

    public async Task<WorkloadRuntimeStatus> GetProxyStatusAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/routes/proxy-status", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadRuntimeStatus>(ct))!;
    }

    public async Task<ProxyStateResponse> GetProxyStateAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/routes/proxy-state", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<ProxyStateResponse>(ct))!;
    }
}
