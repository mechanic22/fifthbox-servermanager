using FifthBox.ServerManager.App.Routes;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfRouteRepository(AppDbContext db) : IRouteRepository
{
    public async Task<IReadOnlyList<Route>> ListAsync(CancellationToken ct = default)
        => await db.Routes.AsNoTracking().OrderBy(r => r.Hostname).ThenBy(r => r.Path).ToListAsync(ct);

    public Task<Route?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.Routes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsAsync(string hostname, string path, string? excludingId = null, CancellationToken ct = default)
        => db.Routes.AnyAsync(r => r.Hostname == hostname && r.Path == path && (excludingId == null || r.Id != excludingId), ct);

    public async Task AddAsync(Route route, CancellationToken ct = default)
    {
        db.Routes.Add(route);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Route route, CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Route route, CancellationToken ct = default)
    {
        db.Routes.Remove(route);
        await db.SaveChangesAsync(ct);
    }
}
