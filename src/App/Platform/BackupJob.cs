using FifthBox.ServerManager.App.Common;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

public sealed class BackupJob(IBackupService backups, IOptions<BackupOptions> options, TimeProvider clock) : IScheduledJob
{
    private readonly BackupOptions _options = options.Value;

    public string Name => "backup";

    public TimeSpan Interval => TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));

    public Task RunAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled || !BackupSchedule.IsDue(backups.List().FirstOrDefault()?.CreatedAt, clock.GetUtcNow(), Interval))
        {
            return Task.CompletedTask;
        }

        return backups.CreateAndPruneAsync(ct);
    }
}
