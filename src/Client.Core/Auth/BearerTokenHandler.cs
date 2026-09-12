using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// The whole native auth lifecycle in one place. Attaches the stored access token to every request;
/// on a 401 it swaps the refresh token for a new pair via <c>POST /api/auth/native/refresh</c> (once)
/// and replays the request. If refresh fails it clears the tokens and signs the session out. The
/// refresh sub-request goes through <c>base.SendAsync</c> — the inner handler — so it never loops back
/// through this one.
/// </summary>
public sealed class BearerTokenHandler(ITokenStore store, IAuthSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var access = await store.GetAccessTokenAsync(ct);
        if (!string.IsNullOrEmpty(access))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        }

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 401 — try a single refresh, then replay the original request once with the new token.
        if (!await TryRefreshAsync(request, ct))
        {
            await store.ClearAsync(ct);
            await session.SignOutAsync();
            return response;
        }

        response.Dispose();
        var retried = CloneRequest(request);
        retried.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await store.GetAccessTokenAsync(ct));
        return await base.SendAsync(retried, ct);
    }

    private async Task<bool> TryRefreshAsync(HttpRequestMessage original, CancellationToken ct)
    {
        var refreshToken = await store.GetRefreshTokenAsync(ct);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var refreshUri = new Uri(new Uri(original.RequestUri!.GetLeftPart(UriPartial.Authority)), "api/auth/native/refresh");
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUri)
        {
            Content = JsonContent.Create(new RefreshRequest { RefreshToken = refreshToken })
        };

        using var refreshResponse = await base.SendAsync(refreshRequest, ct);
        if (!refreshResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var pair = await refreshResponse.Content.ReadFromJsonAsync<NativeAuthResponse>(ct);
        if (pair is null || string.IsNullOrEmpty(pair.AccessToken))
        {
            return false;
        }

        await store.SaveAsync(new TokenPair(pair.AccessToken, pair.RefreshToken, pair.ExpiresAt), ct);
        return true;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version, Content = request.Content };
        foreach (var header in request.Headers)
        {
            if (header.Key != "Authorization")
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        return clone;
    }
}
