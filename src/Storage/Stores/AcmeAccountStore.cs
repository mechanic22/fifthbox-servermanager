using FifthBox.ServerManager.App.Certificates;
using FifthBox.ServerManager.App.Platform;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

/// The ACME account lives on the single platform-settings row. It writes only its own two columns, so
/// saving settings from the UI and registering an account can't overwrite each other.
public sealed class AcmeAccountStore(AppDbContext db) : IAcmeAccountStore
{
    public async Task<StoredAcmeAccount?> GetAsync(CancellationToken ct = default)
    {
        var row = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return row?.AcmeAccountKeyEnc is { Length: > 0 } key && row.AcmeAccountDirectory is { Length: > 0 } directory
            ? new StoredAcmeAccount { Directory = directory, EncryptedKeyPem = key }
            : null;
    }

    public async Task SaveAsync(StoredAcmeAccount account, CancellationToken ct = default)
    {
        var row = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new PlatformSettings();
            db.PlatformSettings.Add(row);
        }

        row.AcmeAccountKeyEnc = account.EncryptedKeyPem;
        row.AcmeAccountDirectory = account.Directory;
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CertificateSecretStore(AppDbContext db) : IProtectedSecretStore
{
    public string Name => "certificate private keys";

    public async Task<string?> SampleAsync(CancellationToken ct = default)
        => await db.Certificates.AsNoTracking()
            .Where(c => c.PrivateKeyEnc != null && c.PrivateKeyEnc != "")
            .Select(c => c.PrivateKeyEnc)
            .FirstOrDefaultAsync(ct);

    public async Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default)
    {
        foreach (var certificate in await db.Certificates.Where(c => c.PrivateKeyEnc != null && c.PrivateKeyEnc != "").ToListAsync(ct))
        {
            certificate.PrivateKeyEnc = rewrite(certificate.PrivateKeyEnc!);
        }

        await db.SaveChangesAsync(ct);
    }
}

public sealed class AcmeAccountSecretStore(AppDbContext db) : IProtectedSecretStore
{
    public string Name => "ACME account key";

    public async Task<string?> SampleAsync(CancellationToken ct = default)
        => await db.PlatformSettings.AsNoTracking()
            .Where(s => s.AcmeAccountKeyEnc != null && s.AcmeAccountKeyEnc != "")
            .Select(s => s.AcmeAccountKeyEnc)
            .FirstOrDefaultAsync(ct);

    public async Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default)
    {
        foreach (var row in await db.PlatformSettings.Where(s => s.AcmeAccountKeyEnc != null && s.AcmeAccountKeyEnc != "").ToListAsync(ct))
        {
            row.AcmeAccountKeyEnc = rewrite(row.AcmeAccountKeyEnc!);
        }

        await db.SaveChangesAsync(ct);
    }
}
