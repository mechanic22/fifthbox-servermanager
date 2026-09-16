using FifthBox.ServerManager.App.Workloads;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfWorkloadGroupRepository(AppDbContext db) : IWorkloadGroupRepository
{
    public async Task<IReadOnlyList<WorkloadGroup>> ListAsync(CancellationToken ct = default)
        => await db.WorkloadGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct);

    public Task<WorkloadGroup?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.WorkloadGroups.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task AddAsync(WorkloadGroup group, CancellationToken ct = default)
    {
        db.WorkloadGroups.Add(group);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(WorkloadGroup group, CancellationToken ct = default)
    {
        db.WorkloadGroups.Update(group);
        return db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(WorkloadGroup group, CancellationToken ct = default)
    {
        db.WorkloadGroups.Remove(group);
        await db.SaveChangesAsync(ct);
    }
}
