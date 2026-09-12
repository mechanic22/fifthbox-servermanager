namespace FifthBox.ServerManager.App.Platform;

public static class BackupSchedule
{
    /// Whether a scheduled backup is warranted, judged from the newest backup already on disk rather than
    /// from the runner's last-run state — that state is per-process, so a restart would otherwise take a
    /// fresh backup every time and churn through the retention window in a fraction of the interval.
    public static bool IsDue(DateTimeOffset? newestBackupAt, DateTimeOffset now, TimeSpan interval) =>
        newestBackupAt is not { } last || now - last >= interval;
}
