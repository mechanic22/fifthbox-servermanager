namespace FifthBox.ServerManager.Shared.Agents;

public record AgentResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public AgentPlatform Platform { get; init; }
    public AgentStatus Status { get; init; }
    public DateTimeOffset EnrolledAt { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }

    /// null until the agent reports once after connecting
    public AgentMetrics? Metrics { get; init; }
}
