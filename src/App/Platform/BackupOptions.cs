namespace FifthBox.ServerManager.App.Platform;

/// default dir is relative so dev runs with no config, the container points it at the data volume
public sealed class BackupOptions
{
    public string Directory { get; set; } = "backups";

    /// pruned after each scheduled run
    public int Keep { get; set; } = 14;

    public int IntervalHours { get; set; } = 24;

    /// scheduled job only, a backup from the UI always runs
    public bool Enabled { get; set; } = true;
}
