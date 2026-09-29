namespace FifthBox.ServerManager.App.Routes;

public interface IWwwRedirectRepository
{
    Task<IReadOnlyList<WwwRedirect>> ListAsync(CancellationToken ct = default);
    Task<WwwRedirect?> FindAsync(string hostname, CancellationToken ct = default);
    Task AddAsync(WwwRedirect redirect, CancellationToken ct = default);
    Task RemoveAsync(WwwRedirect redirect, CancellationToken ct = default);
}
