namespace FifthBox.ServerManager.App.Platform;

/// Persistence for the single PlatformSettings row. Implemented by Storage (EF). Pure persistence.
public interface IPlatformSettingsRepository
{
    Task<PlatformSettings?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(PlatformSettings settings, CancellationToken ct = default);
}
