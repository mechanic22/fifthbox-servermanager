namespace FifthBox.ServerManager.Client.Core;

public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);
    Task SaveAsync(TokenPair tokens, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
