using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>Typed HTTP client for workload groups.</summary>
public interface IWorkloadGroupsClient
{
    Task<IReadOnlyList<WorkloadGroupResponse>> ListAsync(CancellationToken ct = default);
    Task<WorkloadGroupResponse> CreateAsync(CreateWorkloadGroupRequest request, CancellationToken ct = default);
    Task<WorkloadGroupResponse> RenameAsync(string id, UpdateWorkloadGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class WorkloadGroupsClient(HttpClient http) : IWorkloadGroupsClient
{
    public async Task<IReadOnlyList<WorkloadGroupResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/workload-groups", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<WorkloadGroupResponse>>(ct) ?? [];
    }

    public async Task<WorkloadGroupResponse> CreateAsync(CreateWorkloadGroupRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/workload-groups", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadGroupResponse>(ct))!;
    }

    public async Task<WorkloadGroupResponse> RenameAsync(string id, UpdateWorkloadGroupRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/workload-groups/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<WorkloadGroupResponse>(ct))!;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/workload-groups/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }
}
