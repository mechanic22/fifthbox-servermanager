using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Teams;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for the admin-only teams API.
/// </summary>
public interface ITeamsClient
{
    Task<IReadOnlyList<TeamResponse>> ListAsync(CancellationToken ct = default);
    Task<TeamResponse> CreateAsync(SaveTeamRequest request, CancellationToken ct = default);
    Task<TeamResponse> UpdateAsync(string id, SaveTeamRequest request, CancellationToken ct = default);
    Task<TeamResponse> SetMembersAsync(string id, SetTeamMembersRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class TeamsClient(HttpClient http) : ITeamsClient
{
    public async Task<IReadOnlyList<TeamResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/teams", ct);
        return await ReadListAsync(response, ct);
    }

    public async Task<TeamResponse> CreateAsync(SaveTeamRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/teams", request, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<TeamResponse> UpdateAsync(string id, SaveTeamRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/teams/{id}", request, ct);
        return await ReadAsync(response, ct);
    }

    public async Task<TeamResponse> SetMembersAsync(string id, SetTeamMembersRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/teams/{id}/members", request, ct);
        return await ReadAsync(response, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/teams/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    private static async Task<TeamResponse> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<TeamResponse>(ct)
            ?? throw new InvalidOperationException("The server returned an empty team.");
    }

    private static async Task<IReadOnlyList<TeamResponse>> ReadListAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<TeamResponse>>(ct) ?? [];
    }
}
