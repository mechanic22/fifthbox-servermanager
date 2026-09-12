using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Storage;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Host.Infrastructure;

internal static class MigrationBackup
{
    /// Snapshot the database before an upgrade applies migrations. A migration that goes wrong is the one
    /// data-loss path that scheduled backups can miss entirely, since it happens seconds after startup.
    /// Skipped on a fresh database (nothing to lose) and when there's nothing pending.
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
            // Never block startup on this. Losing the safety net is worse than nothing, but refusing to
            // start because a backup directory isn't writable is worse still.
            logger.LogError(ex, "Pre-migration backup failed; continuing");
        }
    }
}
