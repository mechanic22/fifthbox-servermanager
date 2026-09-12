using FifthBox.ServerManager.App.Platform;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

/// EF implementation of the single-row PlatformSettings store. Thin adapter — no business rules.
/// The copy below is field-by-field on purpose: AcmeAccountKeyEnc/AcmeAccountDirectory belong to
/// AcmeAccountStore and must not be written from here. Adding a column to PlatformSettings means
/// adding it here too, or it silently never persists.
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
