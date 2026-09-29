using FifthBox.ServerManager.App.Routes;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfWwwRedirectRepository(AppDbContext db) : IWwwRedirectRepository
{
    public async Task<IReadOnlyList<WwwRedirect>> ListAsync(CancellationToken ct = default)
        => await db.WwwRedirects.AsNoTracking().OrderBy(w => w.Hostname).ToListAsync(ct);

    public Task<WwwRedirect?> FindAsync(string hostname, CancellationToken ct = default)
        => db.WwwRedirects.FirstOrDefaultAsync(w => w.Hostname == hostname, ct);

    public async Task AddAsync(WwwRedirect redirect, CancellationToken ct = default)
    {
        db.WwwRedirects.Add(redirect);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(WwwRedirect redirect, CancellationToken ct = default)
    {
        db.WwwRedirects.Remove(redirect);
        await db.SaveChangesAsync(ct);
    }
}
