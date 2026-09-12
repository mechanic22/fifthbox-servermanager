using FifthBox.ServerManager.App.Registries;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

/// EF implementation of <see cref="IRegistryRepository"/>. Thin adapter — no business rules.
public class EfRegistryRepository(AppDbContext db) : IRegistryRepository
{
    public async Task<IReadOnlyList<Registry>> ListAsync(CancellationToken ct = default)
        => await db.Registries.AsNoTracking().OrderBy(r => r.Domain).ToListAsync(ct);

    public Task<Registry?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.Registries.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Registry registry, CancellationToken ct = default)
    {
        db.Registries.Add(registry);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Registry registry, CancellationToken ct = default)
    {
        db.Registries.Update(registry);
        return db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Registry registry, CancellationToken ct = default)
    {
        db.Registries.Remove(registry);
        await db.SaveChangesAsync(ct);
    }
}
