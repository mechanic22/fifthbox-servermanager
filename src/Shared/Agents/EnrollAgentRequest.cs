namespace FifthBox.ServerManager.Shared.Agents;

/// unauthenticated, gated by the enrollment key
public record EnrollAgentRequest
{
    public string EnrollmentKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AgentPlatform Platform { get; init; }
}
