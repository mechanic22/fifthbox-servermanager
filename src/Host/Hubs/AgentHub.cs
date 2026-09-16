using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Host.Realtime;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// no [Authorize] on purpose, agents send an agentId:secret token we check on connect
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

            // not Context.Abort(), that gives no reason and the agent retries a dead credential forever
            // a HubException message gets through and closes with reconnect off
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
            // not ConnectionAborted, it's already cancelled here
            await statusRelay.RelayOfflineAsync(agentId);
            RefreshMachines();
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task Heartbeat()
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : agents.MarkSeenAsync(agentId, Context.ConnectionAborted);
    }

    /// docker never reports agents coming and going, so poke the machine list ourselves
    /// not awaited so a slow docker call can't stall the connect. it logs its own failures
    private void RefreshMachines() => _ = cluster.RefreshNodesAsync();

    /// not folded into Heartbeat because older agents never call it
    public Task ReportMetrics(AgentMetrics metrics)
    {
        if (registry.AgentIdFor(Context.ConnectionId) is { } agentId)
        {
            registry.Report(agentId, metrics);
        }

        return Task.CompletedTask;
    }

    public Task ReportStatus(AgentWorkloadStatus status)
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : statusRelay.RelayAsync(agentId, status, Context.ConnectionAborted);
    }

    public Task ReportLogs(string workloadName, IReadOnlyList<WorkloadLogLine> lines)
    {
        var agentId = registry.AgentIdFor(Context.ConnectionId);
        return agentId is null ? Task.CompletedTask : statusRelay.RelayLogsAsync(agentId, workloadName, lines, Context.ConnectionAborted);
    }

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
