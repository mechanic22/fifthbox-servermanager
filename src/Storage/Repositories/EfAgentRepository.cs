using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Storage.Models;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfAgentRepository(AppDbContext db) : IAgentRepository
{
    public async Task<IReadOnlyList<Agent>> ListAsync(CancellationToken ct = default)
        => await db.Agents.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);

    public Task<Agent?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.Agents.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Agent agent, CancellationToken ct = default)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Agent agent, CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Agent agent, CancellationToken ct = default)
    {
        db.Agents.Remove(agent);
        await db.SaveChangesAsync(ct);
    }
}

public class EfEnrollmentKeyStore(AppDbContext db) : IEnrollmentKeyStore
{
    private const string RowId = "current";

    public async Task<string?> GetHashAsync(CancellationToken ct = default)
        => (await db.EnrollmentKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == RowId, ct))?.Hash;

    public async Task SetHashAsync(string hash, CancellationToken ct = default)
    {
        var row = await db.EnrollmentKeys.FirstOrDefaultAsync(k => k.Id == RowId, ct);
        if (row is null)
        {
            db.EnrollmentKeys.Add(new EnrollmentKeyRecord { Id = RowId, Hash = hash, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            row.Hash = hash;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
