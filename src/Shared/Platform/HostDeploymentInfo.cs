namespace FifthBox.ServerManager.Shared.Platform;

public enum HostRunMode
{
    /// cli or systemd, no container
    BareProcess,

    /// docker run / compose
    Container,

    /// the managed install
    SwarmService,
}

public record HostDeploymentInfo
{
    public HostRunMode RunMode { get; init; }

    public string? ServiceName { get; init; }

    /// whether or not this process is it
    public bool ManagedServiceExists { get; init; }

    /// running unmanaged while a managed service exists, two processes on one docker socket
    public bool SplitBrain { get; init; }

    /// secrets are placeholders, never real values
    public string InstallCommand { get; init; } = string.Empty;

    /// no Platform:Encryption:Key so we made a throwaway one, anything encrypted dies with the process
    public bool EncryptionKeyEphemeral { get; init; }

    public SecretsState Secrets { get; init; }
}
