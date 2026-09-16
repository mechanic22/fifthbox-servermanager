using FifthBox.ServerManager.Client.Core;

namespace FifthBox.ServerManager.Client.Core.Tests;

/// stateful on purpose, the bearer handler reads back what it just saved during a refresh
internal sealed class InMemoryTokenStore : ITokenStore
{
    public string? Access;
    public string? Refresh;
    public int SaveCount;
    public int ClearCount;

    public InMemoryTokenStore(string? access = null, string? refresh = null)
    {
        Access = access;
        Refresh = refresh;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult(Access);
    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default) => Task.FromResult(Refresh);

    public Task SaveAsync(TokenPair tokens, CancellationToken ct = default)
    {
        Access = tokens.AccessToken;
        Refresh = tokens.RefreshToken;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        Access = null;
        Refresh = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
