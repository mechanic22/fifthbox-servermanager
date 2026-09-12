using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

/// A node the app can place work on. Read live from its backend (the swarm today, agents later),
/// not persisted. The service projects it to a NodeResponse for the wire.
public record Node
{
    public required string Id { get; init; }
    public required string Hostname { get; init; }
    public NodeRole Role { get; init; }
    public NodeStatus Status { get; init; }
    public NodeAvailability Availability { get; init; }
    public NodePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public bool IsLeader { get; init; }
    public string? EngineVersion { get; init; }
    public string? Address { get; init; }

    /// Agent nodes only — swarm reports liveness directly, an agent's is a heartbeat timestamp.
    public DateTimeOffset? LastSeenAt { get; init; }
    public NodeBackendKind Backend { get; init; } = NodeBackendKind.Swarm;
}
