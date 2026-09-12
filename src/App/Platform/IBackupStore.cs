namespace FifthBox.ServerManager.App.Platform;

/// One backup file on disk.
public record BackupFile(string Name, long SizeBytes, DateTimeOffset CreatedAt);

/// Port for reading and writing database backups. Implemented by Storage — it owns the database, so it
/// owns snapshotting it — which keeps the App layer off both the filesystem and the DB driver.
public interface IBackupStore
{
    /// Snapshot the live database into <paramref name="fileName"/>, replacing it if it exists.
    Task<BackupFile> WriteAsync(string directory, string fileName, CancellationToken ct = default);

    IReadOnlyList<BackupFile> List(string directory);

    Stream OpenRead(string directory, string fileName);

    void Delete(string directory, string fileName);
}
