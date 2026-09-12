namespace FifthBox.ServerManager.App.Platform;

public static class BackupRetention
{
    /// Which backups to delete so that at most <paramref name="keep"/> remain, newest kept. A keep count
    /// below one is treated as one — pruning every backup is never what someone meant to configure.
    public static IReadOnlyList<BackupFile> SelectForDeletion(IEnumerable<BackupFile> existing, int keep) =>
        existing
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(Math.Max(1, keep))
            .ToList();
}
