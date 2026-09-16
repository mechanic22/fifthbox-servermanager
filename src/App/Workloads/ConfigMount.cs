namespace FifthBox.ServerManager.App.Workloads;

/// versioned by content hash, so redeploying identical content is a no-op
public record ConfigMount
{
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required string Path { get; init; }      // absolute path in the container
}
