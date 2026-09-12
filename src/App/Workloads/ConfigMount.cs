namespace FifthBox.ServerManager.App.Workloads;

/// A file delivered into a workload's container by the backend (a swarm config object on the swarm).
/// Content is versioned by hash, so redeploying identical content is a no-op.
public record ConfigMount
{
    public required string Name { get; init; }     // logical name, e.g. "nginx"
    public required string Content { get; init; }
    public required string Path { get; init; }      // absolute mount path in the container
}
