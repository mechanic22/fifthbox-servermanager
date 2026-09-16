using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfAccessGrantRepository(AppDbContext db) : IAccessGrantRepository
{
    public async Task<IReadOnlyList<AccessGrant>> ListAsync(CancellationToken ct = default)
        => await db.AccessGrants.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<AccessGrant>> ListForSubjectsAsync(IReadOnlyList<GrantSubject> subjects, CancellationToken ct = default)
    {
        if (subjects.Count == 0)
        {
            return [];
        }

        // split by kind, a tuple Contains won't translate to sql
        var userIds = subjects.Where(s => s.Type == AccessSubject.User).Select(s => s.Id).ToList();
        var teamIds = subjects.Where(s => s.Type == AccessSubject.Team).Select(s => s.Id).ToList();

        return await db.AccessGrants.AsNoTracking()
            .Where(g => (g.SubjectType == AccessSubject.User && userIds.Contains(g.SubjectId))
                     || (g.SubjectType == AccessSubject.Team && teamIds.Contains(g.SubjectId)))
            .ToListAsync(ct);
    }

    public Task<AccessGrant?> FindAsync(GrantSubject subject, AccessScope scope, string targetId, CancellationToken ct = default)
    {
        var (type, id) = subject;
        return db.AccessGrants.FirstOrDefaultAsync(
            g => g.SubjectType == type && g.SubjectId == id && g.Scope == scope && g.TargetId == targetId, ct);
    }

    public Task<AccessGrant?> FindByIdAsync(string id, CancellationToken ct = default)
        => db.AccessGrants.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task AddAsync(AccessGrant grant, CancellationToken ct = default)
    {
        db.AccessGrants.Add(grant);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(AccessGrant grant, CancellationToken ct = default)
    {
        db.AccessGrants.Update(grant);
        return db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(AccessGrant grant, CancellationToken ct = default)
    {
        db.AccessGrants.Remove(grant);
        await db.SaveChangesAsync(ct);
    }

    public Task RemoveForTargetAsync(AccessScope scope, string targetId, CancellationToken ct = default)
        => db.AccessGrants.Where(g => g.Scope == scope && g.TargetId == targetId).ExecuteDeleteAsync(ct);

    public Task RemoveForSubjectAsync(GrantSubject subject, CancellationToken ct = default)
    {
        var (type, id) = subject;
        return db.AccessGrants.Where(g => g.SubjectType == type && g.SubjectId == id).ExecuteDeleteAsync(ct);
    }
}
