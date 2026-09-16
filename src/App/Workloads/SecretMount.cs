namespace FifthBox.ServerManager.App.Workloads;

/// like ConfigMount, but swarm keeps secrets encrypted and only in tmpfs, which private keys need
public record SecretMount
{
    public required string Name { get; init; }
    public required string Content { get; init; }

    /// a file name not a path, swarm mounts secrets by name under /run/secrets
    public required string FileName { get; init; }
}
