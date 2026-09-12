using FifthBox.ServerManager.App.Platform;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

public sealed class RegistrySecretStore(AppDbContext db) : IProtectedSecretStore
{
    public string Name => "registry credentials";

    public async Task<string?> SampleAsync(CancellationToken ct = default)
        => await db.Registries.AsNoTracking()
            .Where(r => r.PasswordEnc != "")
            .Select(r => r.PasswordEnc)
            .FirstOrDefaultAsync(ct);

    public async Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default)
    {
        foreach (var registry in await db.Registries.Where(r => r.PasswordEnc != "").ToListAsync(ct))
        {
            registry.PasswordEnc = rewrite(registry.PasswordEnc);
        }

        await db.SaveChangesAsync(ct);
    }
}
