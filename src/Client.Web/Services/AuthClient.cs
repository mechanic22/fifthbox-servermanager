using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Web.Services;

public sealed class AuthClient(HttpClient http) : IAuthClient
{
    public Task LoginAsync(LoginRequest request, CancellationToken ct = default)
        => PostAsync("api/auth/login", request, ct);

    public Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => PostAsync("api/auth/register", request, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        // best effort, a 401 just means the session was already gone
        using var response = await http.PostAsync("api/auth/logout", content: null, ct);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/users/me", ct);

        // 404 is a cookie for an account that no longer exists, treat as signed out
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(ct);
    }

    public async Task<RegistrationInfoResponse> GetRegistrationInfoAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/auth/registration", ct);
        if (!response.IsSuccessStatusCode)
        {
            // old or down server, assume registration is closed
            return new RegistrationInfoResponse();
        }

        return (await response.Content.ReadFromJsonAsync<RegistrationInfoResponse>(ct)) ?? new RegistrationInfoResponse();
    }

    private async Task PostAsync<T>(string uri, T body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(uri, body, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await HttpProblem.ToExceptionAsync(response, ct);
    }
}
