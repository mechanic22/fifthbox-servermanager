using FifthBox.ServerManager.App.Platform;

namespace FifthBox.ServerManager.App.Tests;

/// stateful on purpose, drift detection writes a fingerprint and reads it back next call
internal sealed class InMemoryPlatformSettings : IPlatformSettingsRepository
{
    public PlatformSettings? Row;
    public int SaveCount;

    public InMemoryPlatformSettings(PlatformSettings? row = null) => Row = row;

    public Task<PlatformSettings?> GetAsync(CancellationToken ct = default) => Task.FromResult(Row);

    public Task SaveAsync(PlatformSettings settings, CancellationToken ct = default)
    {
        Row = settings;
        SaveCount++;
        return Task.CompletedTask;
    }
}
