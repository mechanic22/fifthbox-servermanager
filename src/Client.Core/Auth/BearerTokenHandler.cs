using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Client.Core;

/// on 401 refreshes once and replays, a failed refresh clears tokens and signs out
/// refresh goes through base.SendAsync so it can't loop back through here
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
