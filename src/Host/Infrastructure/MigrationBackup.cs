using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Storage;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Host.Infrastructure;

internal static class MigrationBackup
{
    /// a bad migration happens seconds after startup, before any scheduled backup could catch it
    /// skipped on a fresh db or with nothing pending
    public static async Task BackupBeforeMigrationsAsync(this IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var applied = await db.Database.GetAppliedMigrationsAsync(ct);
            if (!applied.Any() || !(await db.Database.GetPendingMigrationsAsync(ct)).Any())
            {
                return;
            }

            var backup = await scope.ServiceProvider.GetRequiredService<IBackupService>().CreateAsync(ct);
            logger.LogInformation("Backed up to {Backup} before applying migrations", backup.Name);
        }
        catch (Exception ex)
        {
            // never block startup over a backup
            logger.LogError(ex, "Pre-migration backup failed; continuing");
        }
    }
}
