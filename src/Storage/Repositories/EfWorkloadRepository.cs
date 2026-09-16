using FifthBox.ServerManager.App.Workloads;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfWorkloadRepository(AppDbContext db) : IWorkloadRepository
{
    public async Task<IReadOnlyList<Workload>> ListAsync(CancellationToken ct = default)
        => await db.Workloads.AsNoTracking().OrderBy(w => w.Name).ToListAsync(ct);

    public Task<Workload?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.Workloads.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<Workload?> FindByNameAsync(string name, CancellationToken ct = default)
        => db.Workloads.AsNoTracking().FirstOrDefaultAsync(w => w.Name == name, ct);

    public Task<bool> NameExistsAsync(string name, string? excludingId = null, CancellationToken ct = default)
        => db.Workloads.AnyAsync(w => w.Name == name && (excludingId == null || w.Id != excludingId), ct);

    public Task ClearGroupAsync(string groupId, CancellationToken ct = default)
        => db.Workloads.Where(w => w.GroupId == groupId).ExecuteUpdateAsync(s => s.SetProperty(w => w.GroupId, (string?)null), ct);

    public async Task AddAsync(Workload workload, CancellationToken ct = default)
    {
        db.Workloads.Add(workload);
        await db.SaveChangesAsync(ct);
    }

    // already tracked from FindByIdAsync, just flush
    public Task UpdateAsync(Workload workload, CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Workload workload, CancellationToken ct = default)
    {
        db.Workloads.Remove(workload);
        await db.SaveChangesAsync(ct);
    }
}
