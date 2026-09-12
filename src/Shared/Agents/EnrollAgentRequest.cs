namespace FifthBox.ServerManager.Shared.Agents;

/// Sent by an agent (unauthenticated) to join the fleet. Gated by the enrollment key, not a user session.
public record EnrollAgentRequest
{
    public string EnrollmentKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AgentPlatform Platform { get; init; }
}
