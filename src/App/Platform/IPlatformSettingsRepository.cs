namespace FifthBox.ServerManager.App.Platform;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettings?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(PlatformSettings settings, CancellationToken ct = default);
}
