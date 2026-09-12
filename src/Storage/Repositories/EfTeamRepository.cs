using FifthBox.ServerManager.App.Access;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

/// EF implementation of <see cref="ITeamRepository"/>. Thin adapter — no business rules.
public class EfTeamRepository(AppDbContext db) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default)
        => await db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Team>> ListForUserAsync(string userId, CancellationToken ct = default)
    {
        // MemberIds is a JSON column, so the filter can't go to SQL.
        var all = await db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        return [.. all.Where(t => t.MemberIds.Contains(userId, StringComparer.Ordinal))];
    }

    public Task<Team?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(Team team, CancellationToken ct = default)
    {
        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Team team, CancellationToken ct = default)
    {
        db.Teams.Update(team);
        return db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Team team, CancellationToken ct = default)
    {
        db.Teams.Remove(team);
        await db.SaveChangesAsync(ct);
    }
}
