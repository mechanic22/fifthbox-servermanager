using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.App.Agents;

/// online/offline comes from the connection registry, not stored here
public class Agent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public AgentPlatform Platform { get; set; }

    /// sha-256 is fine, it's a high-entropy token not a password
    public string SecretHash { get; set; } = string.Empty;

    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
}
