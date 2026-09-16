namespace FifthBox.ServerManager.App.Platform;

public record BackupFile(string Name, long SizeBytes, DateTimeOffset CreatedAt);

public interface IBackupStore
{
    /// overwrites fileName if it exists
    Task<BackupFile> WriteAsync(string directory, string fileName, CancellationToken ct = default);

    IReadOnlyList<BackupFile> List(string directory);

    Stream OpenRead(string directory, string fileName);

    void Delete(string directory, string fileName);
}
