using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.App.Agents;

/// Tracks which agents are currently connected (by SignalR connection). Runtime state, in-memory —
/// implemented in the Host alongside the AgentHub. Not persisted.
public interface IAgentRegistry
{
    void Add(string agentId, string connectionId);

    /// Remove a connection; returns the agent id that went offline, or null if it wasn't tracked.
    string? Remove(string connectionId);

    bool IsOnline(string agentId);

    string? AgentIdFor(string connectionId);

    /// The live connection for an agent, or null if it isn't connected.
    string? ConnectionFor(string agentId);

    /// The agent's last report about its machine. Kept here rather than persisted: it is worth nothing
    /// once the agent is gone, and writing it every heartbeat would be a database write per agent per
    /// tick for data nobody reads afterwards.
    void Report(string agentId, AgentMetrics metrics);

    AgentMetrics? MetricsFor(string agentId);
}
