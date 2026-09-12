using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.App.Agents;

/// A non-swarm node managed by our custom agent (Windows / native hosts). Persisted. Online/offline is
/// runtime state from the connection registry, not stored here.
public class Agent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public AgentPlatform Platform { get; set; }

    /// SHA-256 hash of the agent's connection secret (high-entropy token — not a password).
    public string SecretHash { get; set; } = string.Empty;

    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
}
