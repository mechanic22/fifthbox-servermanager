using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Auth;
using FifthBox.ServerManager.Shared.Users;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// <see cref="IAuthClient"/> for native (bearer) heads. Talks to <c>/api/auth/native/*</c>, persists
/// the token pair via <see cref="ITokenStore"/>, and keeps <see cref="IAuthSession"/> in step. The
/// <see cref="HttpClient"/> it's handed is composed with <see cref="BearerTokenHandler"/>, so once
/// tokens are stored <see cref="GetCurrentUserAsync"/> just works.
/// </summary>
public sealed class NativeAuthClient(HttpClient http, ITokenStore store, IAuthSession session) : IAuthClient
{
    public async Task LoginAsync(LoginRequest request, CancellationToken ct = default)
        => await EstablishSessionAsync(await PostForTokensAsync("api/auth/native/login", request, ct), ct);

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => await EstablishSessionAsync(await PostForTokensAsync("api/auth/native/register", request, ct), ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var refresh = await store.GetRefreshTokenAsync(ct);
        if (!string.IsNullOrEmpty(refresh))
        {
            // Best-effort server-side revoke; we clear locally regardless of the outcome.
            using var response = await http.PostAsJsonAsync("api/auth/native/revoke", new RefreshRequest { RefreshToken = refresh }, ct);
        }

        await store.ClearAsync(ct);
        await session.SignOutAsync();
    }

    public async Task<UserResponse?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/users/me", ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(ct);
    }

    private async Task<NativeAuthResponse> PostForTokensAsync<TBody>(string uri, TBody body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(uri, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<NativeAuthResponse>(ct))!;
    }

    private async Task EstablishSessionAsync(NativeAuthResponse auth, CancellationToken ct)
    {
        await store.SaveAsync(new TokenPair(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt), ct);
        session.SetUser(await GetCurrentUserAsync(ct));
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

}
