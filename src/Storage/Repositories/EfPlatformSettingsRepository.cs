using FifthBox.ServerManager.App.Platform;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

// field by field on purpose, the Acme* columns belong to AcmeAccountStore
// NOTE: new PlatformSettings column? add it here too or it silently never saves
public class EfPlatformSettingsRepository(AppDbContext db) : IPlatformSettingsRepository
{
    public Task<PlatformSettings?> GetAsync(CancellationToken ct = default)
        => db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);

    public async Task SaveAsync(PlatformSettings settings, CancellationToken ct = default)
    {
        var existing = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == settings.Id, ct);
        if (existing is null)
        {
            db.PlatformSettings.Add(settings);
        }
        else
        {
            existing.RootDomain = settings.RootDomain;
            existing.AcmeEmail = settings.AcmeEmail;
            existing.ManagerPrefix = settings.ManagerPrefix;
            existing.AppliedProxyHash = settings.AppliedProxyHash;
            existing.ProxyAppliedAt = settings.ProxyAppliedAt;
            existing.UpdatedAt = settings.UpdatedAt;
        }

        await db.SaveChangesAsync(ct);
    }
}
