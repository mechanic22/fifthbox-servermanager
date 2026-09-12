using FifthBox.ServerManager.Client.Core;

namespace FifthBox.ServerManager.Client.Core.Tests;

/// <summary>
/// Stateful <see cref="ITokenStore"/> fake — the bearer handler reads back what it just saved during a
/// refresh, so a constant Moq return wouldn't model it. Hand-written per the testing rules for
/// stateful doubles a mocking framework handles poorly.
/// </summary>
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
