using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Cleanup;

namespace FifthBox.ServerManager.Client.Core;

public interface ICleanupClient
{
    Task<ImageCleanupResponse> PreviewImagesAsync(CancellationToken ct = default);
    Task<ImageCleanupResult> RemoveUnusedImagesAsync(CancellationToken ct = default);
}

public sealed class CleanupClient(HttpClient http) : ICleanupClient
{
    public async Task<ImageCleanupResponse> PreviewImagesAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/cleanup/images", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<ImageCleanupResponse>(ct))!;
    }

    public async Task<ImageCleanupResult> RemoveUnusedImagesAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/cleanup/images/remove", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<ImageCleanupResult>(ct))!;
    }
}
