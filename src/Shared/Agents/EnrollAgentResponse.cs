namespace FifthBox.ServerManager.Shared.Agents;

/// Returned once on successful enrollment. The agent persists both and presents "AgentId:Secret" as its
/// connection token thereafter. The Host only stores the secret's hash — this is the only time it's seen.
public record EnrollAgentResponse
{
    public required string AgentId { get; init; }
    public required string Secret { get; init; }
}
