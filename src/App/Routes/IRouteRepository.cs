namespace FifthBox.ServerManager.App.Routes;

public interface IRouteRepository
{
    Task<IReadOnlyList<Route>> ListAsync(CancellationToken ct = default);
    Task<Route?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string hostname, string path, string? excludingId = null, CancellationToken ct = default);
    Task AddAsync(Route route, CancellationToken ct = default);
    Task UpdateAsync(Route route, CancellationToken ct = default);
    Task RemoveAsync(Route route, CancellationToken ct = default);
}
