namespace FifthBox.ServerManager.Shared.Platform;

public record PlatformSettingsResponse
{
    public string? RootDomain { get; init; }
    public string? AcmeEmail { get; init; }
    public string? ManagerPrefix { get; init; }

    /// not a setting, shown so you can find a volume's real name on the host
    public string WorkloadNamePrefix { get; init; } = string.Empty;

    /// from config not the settings row, read-only but the client needs them for links
    public int EdgeHttpPort { get; init; } = 80;
    public int EdgeHttpsPort { get; init; } = 443;
}
