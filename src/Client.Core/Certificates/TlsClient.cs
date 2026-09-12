using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Typed HTTP client for per-hostname TLS. Reads ProblemDetails on failure and throws
/// <see cref="ApiException"/>.
/// </summary>
public interface ITlsClient
{
    Task<TlsOverviewResponse> GetAsync(CancellationToken ct = default);
    Task<CertificateResponse> EnableAsync(string hostname, CancellationToken ct = default);
    Task<CertificateResponse> RetryAsync(string hostname, CancellationToken ct = default);
    Task DisableAsync(string hostname, CancellationToken ct = default);
}

public sealed class TlsClient(HttpClient http) : ITlsClient
{
    public async Task<TlsOverviewResponse> GetAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/tls", ct);
        await ThrowIfFailedAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<TlsOverviewResponse>(ct))!;
    }

    public Task<CertificateResponse> EnableAsync(string hostname, CancellationToken ct = default)
        => PostAsync($"api/tls/{Uri.EscapeDataString(hostname)}", ct);

    public Task<CertificateResponse> RetryAsync(string hostname, CancellationToken ct = default)
        => PostAsync($"api/tls/{Uri.EscapeDataString(hostname)}/retry", ct);

    public async Task DisableAsync(string hostname, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/tls/{Uri.EscapeDataString(hostname)}", ct);
        await ThrowIfFailedAsync(response, ct);
    }

    private async Task<CertificateResponse> PostAsync(string uri, CancellationToken ct)
    {
        using var response = await http.PostAsync(uri, null, ct);
        await ThrowIfFailedAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<CertificateResponse>(ct))!;
    }

    private static async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }
}
