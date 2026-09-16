namespace FifthBox.ServerManager.App.Platform;

public static class BackupRetention
{
    /// newest kept, keep below one counts as one
    public static IReadOnlyList<BackupFile> SelectForDeletion(IEnumerable<BackupFile> existing, int keep) =>
        existing
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(Math.Max(1, keep))
            .ToList();
}
