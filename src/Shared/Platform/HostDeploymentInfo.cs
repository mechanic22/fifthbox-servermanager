namespace FifthBox.ServerManager.Shared.Platform;

/// How the Host process itself is running.
public enum HostRunMode
{
    /// Straight from the CLI or a systemd unit — no container.
    BareProcess,

    /// A plain container (docker run / compose).
    Container,

    /// A swarm service task — the managed install.
    SwarmService,
}

/// What the Host knows about its own deployment, so the System page can tell you whether it's managed
/// and what to run if it isn't.
public record HostDeploymentInfo
{
    public HostRunMode RunMode { get; init; }

    /// Swarm service name, when running as one.
    public string? ServiceName { get; init; }

    /// True when a swarm service for the Host exists — whether or not *this* process is it.
    public bool ManagedServiceExists { get; init; }

    /// Set when the Host is running unmanaged while a managed service also exists: two processes on one
    /// Docker socket, and one database if they share the volume.
    public bool SplitBrain { get; init; }

    /// Shell command to install (or re-install) the Host as a swarm service. Secrets appear as
    /// placeholders — never real values.
    public string InstallCommand { get; init; } = string.Empty;

    /// True when no Platform:Encryption:Key was configured and the Host generated a throwaway one.
    /// Anything encrypted under it dies with the process.
    public bool EncryptionKeyEphemeral { get; init; }

    public SecretsState Secrets { get; init; }
}
