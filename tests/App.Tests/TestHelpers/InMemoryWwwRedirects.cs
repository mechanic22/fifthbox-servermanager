using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.App.Tests;

/// stateful on purpose, toggling adds/removes and the next read has to see it
internal sealed class InMemoryWwwRedirects(params string[] hostnames) : IWwwRedirectRepository
{
    public readonly List<WwwRedirect> Rows = [.. hostnames.Select(h => new WwwRedirect { Hostname = h })];

    public Task<IReadOnlyList<WwwRedirect>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WwwRedirect>>([.. Rows]);

    public Task<WwwRedirect?> FindAsync(string hostname, CancellationToken ct = default)
        => Task.FromResult(Rows.FirstOrDefault(r => r.Hostname == hostname));

    public Task AddAsync(WwwRedirect redirect, CancellationToken ct = default)
    {
        Rows.Add(redirect);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(WwwRedirect redirect, CancellationToken ct = default)
    {
        Rows.Remove(redirect);
        return Task.CompletedTask;
    }
}
