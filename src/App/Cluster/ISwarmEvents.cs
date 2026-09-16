namespace FifthBox.ServerManager.App.Cluster;

/// only says what changed, consumers re-read so a missed or duplicate event is harmless
public interface ISwarmEvents
{
    IAsyncEnumerable<SwarmChange> WatchAsync(CancellationToken ct = default);
}

public enum SwarmChangeKind
{
    Node,
    Workload,
}

/// ServiceName is null for a node change
public readonly record struct SwarmChange(SwarmChangeKind Kind, string? ServiceName);
