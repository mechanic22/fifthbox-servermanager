namespace FifthBox.ServerManager.Shared.Agents;

/// only time the secret is ever seen, we just keep the hash
/// agent sends "AgentId:Secret" as its token from then on
public record EnrollAgentResponse
{
    public required string AgentId { get; init; }
    public required string Secret { get; init; }
}
