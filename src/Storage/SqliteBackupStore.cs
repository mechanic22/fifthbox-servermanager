using FifthBox.ServerManager.App.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage;

/// sqlite online backup api, safe while the app is writing
public sealed class SqliteBackupStore(AppDbContext db) : IBackupStore
{
    public async Task<BackupFile> WriteAsync(string directory, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        var source = (SqliteConnection)db.Database.GetDbConnection();
        if (source.State != System.Data.ConnectionState.Open)
        {
            await source.OpenAsync(ct);
        }

        await using var destination = new SqliteConnection($"Data Source={path}");
        await destination.OpenAsync(ct);
        source.BackupDatabase(destination);

        return Describe(new FileInfo(path));
    }

    public IReadOnlyList<BackupFile> List(string directory) =>
        Directory.Exists(directory)
            ? new DirectoryInfo(directory).GetFiles("*.db").Select(Describe).ToList()
            : [];

    public Stream OpenRead(string directory, string fileName) =>
        File.OpenRead(Path.Combine(directory, fileName));

    public void Delete(string directory, string fileName) =>
        File.Delete(Path.Combine(directory, fileName));

    private static BackupFile Describe(FileInfo f) => new(f.Name, f.Length, f.CreationTimeUtc);
}
