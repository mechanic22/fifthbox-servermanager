using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for agent management. Reads ProblemDetails on failure and throws
/// <see cref="ApiException"/>.
/// </summary>
public interface IAgentsClient
{
    Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<EnrollmentKeyResponse> GenerateEnrollmentKeyAsync(CancellationToken ct = default);
}

public sealed class AgentsClient(HttpClient http) : IAgentsClient
{
    public async Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/agents", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<AgentResponse>>(ct) ?? [];
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/agents/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task<EnrollmentKeyResponse> GenerateEnrollmentKeyAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/agents/enrollment-key", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<EnrollmentKeyResponse>(ct))!;
    }
}
