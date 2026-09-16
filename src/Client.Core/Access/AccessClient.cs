using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.Client.Core;

public interface IAccessClient
{
    Task<IReadOnlyList<AccessGrantResponse>> ListGrantsAsync(CancellationToken ct = default);

    /// includes access inherited from groups above
    Task<IReadOnlyList<AccessGrantResponse>> ListGrantsForTargetAsync(
        AccessScope scope, string targetId, CancellationToken ct = default);

    /// upsert, null back when level None removed it
    Task<AccessGrantResponse?> SetGrantAsync(SetAccessGrantRequest request, CancellationToken ct = default);

    Task RemoveGrantAsync(string id, CancellationToken ct = default);
}

public sealed class AccessClient(HttpClient http) : IAccessClient
{
    public async Task<IReadOnlyList<AccessGrantResponse>> ListGrantsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/access/grants", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<AccessGrantResponse>>(ct) ?? [];
    }

    public async Task<IReadOnlyList<AccessGrantResponse>> ListGrantsForTargetAsync(
        AccessScope scope, string targetId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/access/grants/{scope}/{targetId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<AccessGrantResponse>>(ct) ?? [];
    }

    public async Task<AccessGrantResponse?> SetGrantAsync(SetAccessGrantRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync("api/access/grants", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<AccessGrantResponse>(ct);
    }

    public async Task RemoveGrantAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/access/grants/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }
}
