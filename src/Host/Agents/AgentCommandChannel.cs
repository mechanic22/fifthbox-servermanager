using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Host.Hubs;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Agents;

public sealed class AgentCommandChannel(IHubContext<AgentHub> hub, IAgentRegistry registry) : IAgentCommandChannel
{
    public bool IsConnected(string agentId) => registry.IsOnline(agentId);

    public Task<AgentWorkloadStatus> DeployAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default)
        => InvokeAsync(agentId, "Deploy", spec, ct);

    public Task<AgentWorkloadStatus> StopAsync(string agentId, string workloadName, CancellationToken ct = default)
        => InvokeAsync(agentId, "Stop", workloadName, ct);

    public Task<AgentWorkloadStatus> GetStatusAsync(string agentId, string workloadName, CancellationToken ct = default)
        => InvokeAsync(agentId, "GetStatus", workloadName, ct);

    public Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(string agentId, string workloadName, int tail, CancellationToken ct = default)
    {
        var connectionId = registry.ConnectionFor(agentId)
            ?? throw new ConflictException($"Agent '{agentId}' is not connected.");
        return hub.Clients.Client(connectionId)
            .InvokeAsync<IReadOnlyList<WorkloadLogLine>>("GetLogs", workloadName, tail, ct);
    }

    public Task<AgentWorkloadStatus> SendConsoleAsync(string agentId, string workloadName, string text, CancellationToken ct = default)
    {
        var connectionId = registry.ConnectionFor(agentId)
            ?? throw new ConflictException($"Agent '{agentId}' is not connected.");
        return hub.Clients.Client(connectionId)
            .InvokeAsync<AgentWorkloadStatus>("SendConsole", workloadName, text, ct);
    }

    public Task<AgentWorkloadStatus> UpdateAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default)
        => InvokeAsync(agentId, "Update", spec, ct);

    public Task<IReadOnlyList<WorkloadFileEntry>> ListFilesAsync(string agentId, string workloadName, string path, CancellationToken ct = default)
        => Connection(agentId).InvokeAsync<IReadOnlyList<WorkloadFileEntry>>("ListFiles", workloadName, path, ct);

    public Task<WorkloadFileContent> ReadFileAsync(string agentId, string workloadName, string path, CancellationToken ct = default)
        => Connection(agentId).InvokeAsync<WorkloadFileContent>("ReadFile", workloadName, path, ct);

    public Task WriteFileAsync(string agentId, string workloadName, string path, string text, CancellationToken ct = default)
        => Connection(agentId).InvokeAsync<bool>("WriteFile", workloadName, path, text, ct);

    private ISingleClientProxy Connection(string agentId) => hub.Clients.Client(
        registry.ConnectionFor(agentId) ?? throw new ConflictException($"Agent '{agentId}' is not connected."));

    public Task StartFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default)
        => SendAsync(agentId, "StartFollowingLogs", workloadName, ct);

    public Task StopFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default)
        => SendAsync(agentId, "StopFollowingLogs", workloadName, ct);

    private Task SendAsync(string agentId, string method, object argument, CancellationToken ct)
    {
        var connectionId = registry.ConnectionFor(agentId);
        return connectionId is null
            ? Task.CompletedTask
            : hub.Clients.Client(connectionId).SendAsync(method, argument, ct);
    }

    private Task<AgentWorkloadStatus> InvokeAsync(string agentId, string method, object argument, CancellationToken ct)
    {
        var connectionId = registry.ConnectionFor(agentId)
            ?? throw new ConflictException($"Agent '{agentId}' is not connected.");
        return hub.Clients.Client(connectionId).InvokeAsync<AgentWorkloadStatus>(method, argument, ct);
    }
}
