using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Web.Services;

/// <summary>
/// The browser (cookie) <see cref="IAuthClient"/>. Login/register sign a cookie the browser then
/// carries automatically — the client never handles a token. The native bearer variant lives in
/// Client.Core (<c>NativeAuthClient</c>); the shared typed clients and <see cref="ApiException"/> come
/// from there too.
/// </summary>
public sealed class AuthClient(HttpClient http) : IAuthClient
{
    public Task LoginAsync(LoginRequest request, CancellationToken ct = default)
        => PostAsync("api/auth/login", request, ct);

    public Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => PostAsync("api/auth/register", request, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        // Best-effort: a 401 just means the session was already gone.
        using var response = await http.PostAsync("api/auth/logout", content: null, ct);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/users/me", ct);

        // No valid session → treat as signed out. 401: not authenticated. 404: authenticated by a
        // cookie whose account no longer exists — a stale session, not a crash.
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
            // An older server, or one that's down. Assume closed: offering a path that 403s is worse
            // than hiding one that would have worked.
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
