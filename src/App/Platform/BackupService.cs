using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Platform;

public interface IBackupService
{
    Task<BackupFileResponse> CreateAsync(CancellationToken ct = default);
    IReadOnlyList<BackupFileResponse> List();
    Stream OpenRead(string name);
    void Delete(string name);

    Task<BackupFileResponse> CreateAndPruneAsync(CancellationToken ct = default);
}

public sealed class BackupService(
    IBackupStore store,
    IOptions<BackupOptions> options,
    TimeProvider clock) : IBackupService
{
    private readonly BackupOptions _options = options.Value;

    public async Task<BackupFileResponse> CreateAsync(CancellationToken ct = default)
    {
        var name = $"fbsm-{clock.GetUtcNow():yyyyMMdd-HHmmss}.db";
        return Map(await store.WriteAsync(_options.Directory, name, ct));
    }

    public IReadOnlyList<BackupFileResponse> List() =>
        store.List(_options.Directory)
            .OrderByDescending(f => f.CreatedAt)
            .Select(Map)
            .ToList();

    public Stream OpenRead(string name) => store.OpenRead(_options.Directory, Existing(name));

    public void Delete(string name) => store.Delete(_options.Directory, Existing(name));

    public async Task<BackupFileResponse> CreateAndPruneAsync(CancellationToken ct = default)
    {
        var created = await CreateAsync(ct);

        foreach (var stale in BackupRetention.SelectForDeletion(store.List(_options.Directory), _options.Keep))
        {
            store.Delete(_options.Directory, stale.Name);
        }

        return created;
    }

    private string Existing(string name)
    {
        var file = Validate(name);
        if (!store.List(_options.Directory).Any(f => string.Equals(f.Name, file, StringComparison.Ordinal)))
        {
            throw new NotFoundException($"Backup '{file}' not found.");
        }

        return file;
    }

    // name comes straight off a route, must be a bare filename so it can't climb out of the backup dir
    private static string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || !name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(nameof(name), "Not a backup file name.");
        }

        return name;
    }

    private static BackupFileResponse Map(BackupFile f) => new()
    {
        Name = f.Name,
        SizeBytes = f.SizeBytes,
        CreatedAt = f.CreatedAt,
    };
}
