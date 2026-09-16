using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Host.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, string> _byConnection = new();
    private readonly ConcurrentDictionary<string, AgentMetrics> _metrics = new(StringComparer.Ordinal);

    public void Add(string agentId, string connectionId) => _byConnection[connectionId] = agentId;

    public string? Remove(string connectionId)
    {
        if (!_byConnection.TryRemove(connectionId, out var agentId))
        {
            return null;
        }

        // drop metrics with the connection so stale numbers don't look current
        _metrics.TryRemove(agentId, out _);
        return agentId;
    }

    public bool IsOnline(string agentId) => _byConnection.Values.Contains(agentId);

    public string? AgentIdFor(string connectionId) => _byConnection.TryGetValue(connectionId, out var agentId) ? agentId : null;

    public string? ConnectionFor(string agentId) => _byConnection.FirstOrDefault(kv => kv.Value == agentId).Key;

    public void Report(string agentId, AgentMetrics metrics) => _metrics[agentId] = metrics;

    public AgentMetrics? MetricsFor(string agentId) => _metrics.GetValueOrDefault(agentId);
}
