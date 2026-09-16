namespace FifthBox.ServerManager.App.Platform;

public static class BackupSchedule
{
    /// judged off the newest backup on disk, runner state is per-process so a restart would back up every time
    public static bool IsDue(DateTimeOffset? newestBackupAt, DateTimeOffset now, TimeSpan interval) =>
        newestBackupAt is not { } last || now - last >= interval;
}
