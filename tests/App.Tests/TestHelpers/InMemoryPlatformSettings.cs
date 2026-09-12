using FifthBox.ServerManager.App.Platform;

namespace FifthBox.ServerManager.App.Tests;

/// <summary>
/// Stateful <see cref="IPlatformSettingsRepository"/> fake. Drift detection writes a fingerprint and
/// reads it back on the next call, which a constant Moq return can't model. Hand-written per the
/// testing rules for stateful doubles.
/// </summary>
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
