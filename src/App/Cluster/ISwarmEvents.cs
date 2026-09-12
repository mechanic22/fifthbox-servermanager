namespace FifthBox.ServerManager.App.Cluster;

/// A live feed of "something changed" from the container backend. Deliberately says nothing about *what*
/// changed beyond the subject — the consumer re-reads state rather than trusting a delta, so a missed or
/// duplicated notification costs a redundant read and never a wrong answer.
public interface ISwarmEvents
{
    IAsyncEnumerable<SwarmChange> WatchAsync(CancellationToken ct = default);
}

public enum SwarmChangeKind
{
    Node,
    Workload,
}

/// ServiceName is the docker service name for a workload change, null for a node change.
public readonly record struct SwarmChange(SwarmChangeKind Kind, string? ServiceName);
