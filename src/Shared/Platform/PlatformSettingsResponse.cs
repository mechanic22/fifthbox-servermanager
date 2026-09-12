namespace FifthBox.ServerManager.Shared.Platform;

public record PlatformSettingsResponse
{
    public string? RootDomain { get; init; }
    public string? AcmeEmail { get; init; }
    public string? ManagerPrefix { get; init; }

    /// What the platform namespaces a workload's docker objects with. Not a setting — the client shows
    /// it so a volume's real name on the host isn't a secret only the backend knows.
    public string WorkloadNamePrefix { get; init; } = string.Empty;

    /// The ports the edge binds, from configuration rather than the settings row — read-only here, but
    /// the client needs them to build links that actually open.
    public int EdgeHttpPort { get; init; } = 80;
    public int EdgeHttpsPort { get; init; } = 443;
}
