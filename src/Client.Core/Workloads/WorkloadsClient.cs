using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for workload definitions. Reads ProblemDetails on failure and throws
/// <see cref="ApiException"/> with a user-safe message (validation errors surface as their text).
/// </summary>
public interface IWorkloadsClient
{
    Task<IReadOnlyList<WorkloadResponse>> ListAsync(CancellationToken ct = default);
    Task<WorkloadResponse> GetAsync(string id, CancellationToken ct = default);
    Task<WorkloadResponse> CreateAsync(CreateWorkloadRequest request, CancellationToken ct = default);
    Task<WorkloadResponse> UpdateAsync(string id, UpdateWorkloadRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    Task<WorkloadRuntimeStatus> GetStatusAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetStatusesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(string id, int tail = 200, CancellationToken ct = default);

    /// This workload's routes only. Works for a granted non-admin, unlike listing every route.
    Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(string id, CancellationToken ct = default);

    /// <summary>Agents this workload could move to. Needs Configure on the workload.</summary>
    Task<IReadOnlyList<AgentResponse>> GetMoveTargetsAsync(string id, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> DeployAsync(string id, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> ScaleAsync(string id, int replicas, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> RestartAsync(string id, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> StartAsync(string id, CancellationToken ct = default);
    Task StopAsync(string id, CancellationToken ct = default);

    /// Send one line to a native workload's console.
    Task SendConsoleAsync(string id, string text, CancellationToken ct = default);

    /// Start fetching a native workload's files. Returns once the acquire has started, not finished.
    Task<WorkloadRuntimeStatus> UpdateAsync(string id, CancellationToken ct = default);

    /// Browse and edit the files under a managed workload's directory.
    Task<IReadOnlyList<WorkloadFileEntry>> ListFilesAsync(string id, string? path, CancellationToken ct = default);
    Task<WorkloadFileContent> ReadFileAsync(string id, string path, CancellationToken ct = default);
    Task WriteFileAsync(string id, string path, string text, CancellationToken ct = default);

    Task UndeployAsync(string id, CancellationToken ct = default);

    Task<WorkloadDriftResponse> GetDriftAsync(string id, CancellationToken ct = default);
    Task<MoveWorkloadResponse> MoveAsync(string id, string agentId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkloadRevisionResponse>> GetRevisionsAsync(string id, CancellationToken ct = default);
    Task<WorkloadResponse> RevertAsync(string id, int number, CancellationToken ct = default);
}

public sealed class WorkloadsClient(HttpClient http) : IWorkloadsClient
{
    public async Task<IReadOnlyList<WorkloadResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/workloads", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<WorkloadResponse>>(ct) ?? [];
    }

    public async Task<WorkloadResponse> GetAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadResponse>(ct))!;
    }

    public Task<WorkloadResponse> CreateAsync(CreateWorkloadRequest request, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, "api/workloads", request, ct);

    public Task<WorkloadResponse> UpdateAsync(string id, UpdateWorkloadRequest request, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, $"api/workloads/{id}", request, ct);

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/workloads/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public Task<WorkloadRuntimeStatus> GetStatusAsync(string id, CancellationToken ct = default)
        => SendForStatusAsync<object>($"api/workloads/{id}/status", HttpMethod.Get, null, ct);

    public async Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetStatusesAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/workloads/statuses", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<Dictionary<string, WorkloadRuntimeStatus>>(ct) ?? [];
    }

    public Task<WorkloadRuntimeStatus> DeployAsync(string id, CancellationToken ct = default)
        => SendForStatusAsync<object>($"api/workloads/{id}/deploy", HttpMethod.Post, null, ct);

    public Task<WorkloadRuntimeStatus> ScaleAsync(string id, int replicas, CancellationToken ct = default)
        => SendForStatusAsync($"api/workloads/{id}/scale", HttpMethod.Post, new ScaleWorkloadRequest { Replicas = replicas }, ct);

    public Task<WorkloadRuntimeStatus> RestartAsync(string id, CancellationToken ct = default)
        => SendForStatusAsync<object>($"api/workloads/{id}/restart", HttpMethod.Post, null, ct);

    public Task<WorkloadRuntimeStatus> StartAsync(string id, CancellationToken ct = default)
        => SendForStatusAsync<object>($"api/workloads/{id}/start", HttpMethod.Post, null, ct);

    public Task StopAsync(string id, CancellationToken ct = default) => PostAsync($"api/workloads/{id}/stop", ct);

    public Task<WorkloadRuntimeStatus> UpdateAsync(string id, CancellationToken ct = default)
        => SendForStatusAsync<object>($"api/workloads/{id}/update", HttpMethod.Post, null, ct);

    public async Task<IReadOnlyList<WorkloadFileEntry>> ListFilesAsync(string id, string? path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/files?path={Uri.EscapeDataString(path ?? string.Empty)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkloadFileEntry>>(ct))!;
    }

    public async Task<WorkloadFileContent> ReadFileAsync(string id, string path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/files/content?path={Uri.EscapeDataString(path)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadFileContent>(ct))!;
    }

    public async Task WriteFileAsync(string id, string path, string text, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/workloads/{id}/files/content",
            new WriteWorkloadFileRequest { Path = path, Text = text }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task SendConsoleAsync(string id, string text, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"api/workloads/{id}/console", new SendConsoleRequest { Text = text }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public Task UndeployAsync(string id, CancellationToken ct = default) => PostAsync($"api/workloads/{id}/undeploy", ct);

    public async Task<WorkloadDriftResponse> GetDriftAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/drift", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadDriftResponse>(ct))!;
    }

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var response = await http.PostAsync(url, null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task<MoveWorkloadResponse> MoveAsync(string id, string agentId, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"api/workloads/{id}/move", new MoveWorkloadRequest { AgentId = agentId }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<MoveWorkloadResponse>(ct))!;
    }

    public async Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(string id, int tail = 200, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/logs?tail={tail}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<WorkloadLogLine>>(ct))!;
    }

    public async Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/routes", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<RouteResponse>>(ct))!;
    }

    public async Task<IReadOnlyList<AgentResponse>> GetMoveTargetsAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/move-targets", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<AgentResponse>>(ct))!;
    }

    public async Task<IReadOnlyList<WorkloadRevisionResponse>> GetRevisionsAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/workloads/{id}/revisions", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<WorkloadRevisionResponse>>(ct) ?? [];
    }

    public Task<WorkloadResponse> RevertAsync(string id, int number, CancellationToken ct = default)
        => SendAsync<object?>(HttpMethod.Post, $"api/workloads/{id}/revert/{number}", null, ct);

    private async Task<WorkloadResponse> SendAsync<TBody>(HttpMethod method, string uri, TBody body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadResponse>(ct))!;
    }

    private async Task<WorkloadRuntimeStatus> SendForStatusAsync<TBody>(string uri, HttpMethod method, TBody? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadRuntimeStatus>(ct))!;
    }
}
