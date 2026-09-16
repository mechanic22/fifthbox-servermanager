using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Registries;

namespace FifthBox.ServerManager.Client.Core;

public interface IRegistriesClient
{
    Task<IReadOnlyList<RegistryResponse>> ListAsync(CancellationToken ct = default);
    Task<RegistryResponse> CreateAsync(CreateRegistryRequest request, CancellationToken ct = default);
    Task<RegistryResponse> UpdateAsync(string id, UpdateRegistryRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class RegistriesClient(HttpClient http) : IRegistriesClient
{
    public async Task<IReadOnlyList<RegistryResponse>> ListAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/registries", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<RegistryResponse>>(ct) ?? [];
    }

    public async Task<RegistryResponse> CreateAsync(CreateRegistryRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/registries", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RegistryResponse>(ct))!;
    }

    public async Task<RegistryResponse> UpdateAsync(string id, UpdateRegistryRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"api/registries/{id}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RegistryResponse>(ct))!;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/registries/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }
}
