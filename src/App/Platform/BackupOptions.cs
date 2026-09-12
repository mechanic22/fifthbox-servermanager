namespace FifthBox.ServerManager.App.Platform;

/// Bound from the "Backup" config section. The default directory is relative so a dev run works with no
/// configuration; the container sets it to a path inside the mounted data volume.
public sealed class BackupOptions
{
    public string Directory { get; set; } = "backups";

    /// How many backups to keep. Older ones are pruned after each scheduled run.
    public int Keep { get; set; } = 14;

    public int IntervalHours { get; set; } = 24;

    /// Turns off the scheduled job only — a backup taken from the UI always runs.
    public bool Enabled { get; set; } = true;
}
