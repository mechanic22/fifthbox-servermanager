using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Host.Realtime;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// The agent-facing connection. Agents authenticate with an "agentId:secret" token (not a user session),
/// so this hub is deliberately not [Authorize]'d — it validates the token itself on connect and aborts
/// anything that fails. Tracks presence via the registry.
public sealed class AgentHub(
    IAgentService agents,
    IAgentRegistry registry,
    IAgentDesiredState desiredState,
    IAgentStatusRelay statusRelay,
    IClusterStateWriter cluster,
    ILogger<AgentHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var (agentId, secret) = ReadToken();
        if (agentId is null || secret is null || !await agents.AuthenticateAsync(agentId, secret, Context.ConnectionAborted))
        {
            logger.LogWarning("Rejected agent connection {ConnectionId}", Context.ConnectionId);

            // Not Context.Abort(): that drops the socket with no reason, so the agent sees only whatever
            // call happened to be in flight getting cancelled and reconnects forever with a credential
            // that can never work. A HubException's message reaches the client even without detailed
            // errors, and closes with allow-reconnect off.
            throw new HubException(AgentRejection.Message(
                "this agent is not enrolled here, or its credential no longer matches. It may have been " +
                "removed. Delete agent-credentials.json beside the agent and restart it to enroll again."));
        }

        registry.Add(agentId, Context.ConnectionId);
        await agents.MarkSeenAsync(agentId, Context.ConnectionAborted);

        RefreshMachines();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = registry.Remove(Context.ConnectionId);

        if (agentId is not null)
        {
            // Not Context.ConnectionAborted — it is already cancelled by the time we get here.
            await statusRelay.RelayOfflineAsync(agentId);
            RefreshMachines();
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// Agents call this periodically to refresh last-seen.
    public Task Heartbeat()
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : agents.MarkSeenAsync(agentId, Context.ConnectionAborted);
    }

    /// An agent arriving or leaving is the one node change docker never reports, so the machine list has
    /// to be told. Not awaited: the refresh reads the swarm, and blocking a connect on a slow docker call
    /// would stall the agent for reasons that have nothing to do with it. Clients learn from the broadcast
    /// either way, and RefreshNodesAsync swallows and logs its own failures.
    private void RefreshMachines() => _ = cluster.RefreshNodesAsync();

    /// Agents call this alongside the heartbeat. Older agents never call it, which is why it is not
    /// folded into Heartbeat itself.
    public Task ReportMetrics(AgentMetrics metrics)
    {
        if (registry.AgentIdFor(Context.ConnectionId) is { } agentId)
        {
            registry.Report(agentId, metrics);
        }

        return Task.CompletedTask;
    }

    /// Agents call this whenever one of their processes starts, exits or is restarted.
    public Task ReportStatus(AgentWorkloadStatus status)
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : statusRelay.RelayAsync(agentId, status, Context.ConnectionAborted);
    }

    /// Agents call this while the Host has asked them to follow a workload.
    public Task ReportLogs(string workloadName, IReadOnlyList<WorkloadLogLine> lines)
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : statusRelay.RelayLogsAsync(agentId, workloadName, lines, Context.ConnectionAborted);
    }

    /// Agents call this on connect and after every reconnect to find out what they should be running.
    public Task<IReadOnlyList<AgentWorkloadSpec>> Reconcile()
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null
            ? Task.FromResult<IReadOnlyList<AgentWorkloadSpec>>([])
            : desiredState.ForAgentAsync(agentId, Context.ConnectionAborted);
    }

    private (string? agentId, string? secret) ReadToken()
    {
        var http = Context.GetHttpContext();
        var token = http?.Request.Query["access_token"].ToString();

        if (string.IsNullOrEmpty(token))
        {
            var auth = http?.Request.Headers.Authorization.ToString();
            if (auth is not null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = auth["Bearer ".Length..];
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return (null, null);
        }

        var separator = token.IndexOf(':');
        return separator <= 0 ? (null, null) : (token[..separator], token[(separator + 1)..]);
    }
}
