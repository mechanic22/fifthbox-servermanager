namespace FifthBox.ServerManager.App.Routes;

/// Persistence port for route definitions. Implemented by Storage (EF). Pure persistence — no rules.
public interface IRouteRepository
{
    Task<IReadOnlyList<Route>> ListAsync(CancellationToken ct = default);
    Task<Route?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string hostname, string path, string? excludingId = null, CancellationToken ct = default);
    Task AddAsync(Route route, CancellationToken ct = default);
    Task UpdateAsync(Route route, CancellationToken ct = default);
    Task RemoveAsync(Route route, CancellationToken ct = default);
}
