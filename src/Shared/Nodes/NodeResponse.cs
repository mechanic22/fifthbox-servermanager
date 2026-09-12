namespace FifthBox.ServerManager.Shared.Nodes;

public record NodeResponse
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
    public NodeBackendKind Backend { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }
}
