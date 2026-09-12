namespace FifthBox.ServerManager.App.Workloads;

/// A secret file delivered into a workload's container (a swarm secret on the swarm). Same shape as
/// <see cref="ConfigMount"/> and versioned by content hash the same way — the difference is that swarm
/// keeps secrets encrypted in its raft store and only ever materialises them in tmpfs on the nodes
/// running the task, which is what private keys need and config objects don't give.
public record SecretMount
{
    public required string Name { get; init; }
    public required string Content { get; init; }

    /// Filename under the container's secrets directory (/run/secrets). Swarm mounts secrets by name,
    /// not at an arbitrary path, so this is a file name rather than a full path.
    public required string FileName { get; init; }
}
