using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.App.Agents;

public interface IAgentRegistry
{
    void Add(string agentId, string connectionId);

    /// returns the agent id that went offline, null if untracked
    string? Remove(string connectionId);

    bool IsOnline(string agentId);

    string? AgentIdFor(string connectionId);

    /// null if not connected
    string? ConnectionFor(string agentId);

    /// in memory only, a db write per agent per heartbeat for data nobody reads isn't worth it
    void Report(string agentId, AgentMetrics metrics);

    AgentMetrics? MetricsFor(string agentId);
}
