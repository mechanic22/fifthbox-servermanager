namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// The device's token storage. A platform capability: implemented per head (MAUI SecureStorage on
/// device). Client.Core owns the contract so token <em>use</em> — the bearer handler — stays shared
/// and platform-agnostic while token <em>storage</em> varies per platform.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);
    Task SaveAsync(TokenPair tokens, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

/// <summary>An access token, the refresh token that renews it, and when the access token expires.</summary>
public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
