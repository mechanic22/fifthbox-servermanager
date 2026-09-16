using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Agents;

public sealed class AgentBackend(IAgentCommandChannel channel) : IWorkloadBackend
{
    public WorkloadKind SupportedKind => WorkloadKind.Native;

    public async Task DeployAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var agentId = RequireAgent(deployment);
        await channel.DeployAsync(agentId, AgentWorkloadSpecMapper.ToSpec(deployment), ct);
    }

    // single process, nothing to scale
    public Task ScaleAsync(WorkloadDeployment deployment, int replicas, CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task RestartAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var agentId = RequireAgent(deployment);
        await channel.StopAsync(agentId, deployment.Name, ct);
        await channel.DeployAsync(agentId, AgentWorkloadSpecMapper.ToSpec(deployment), ct);
    }

    public async Task StopAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var agentId = RequireAgent(deployment);
        await channel.StopAsync(agentId, deployment.Name, ct);
    }

    public Task StartAsync(WorkloadDeployment deployment, CancellationToken ct = default)
        => DeployAsync(deployment, ct);

    public Task UndeployAsync(WorkloadDeployment deployment, CancellationToken ct = default) =>
        StopAsync(deployment, ct);

    public async Task<WorkloadRuntimeStatus> GetStatusAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        if (deployment.AgentId is null || !channel.IsConnected(deployment.AgentId))
        {
            return new WorkloadRuntimeStatus { Name = deployment.Name, Deployed = false, State = WorkloadState.NotDeployed };
        }

        var status = await channel.GetStatusAsync(deployment.AgentId, deployment.Name, ct);
        return AgentStatusMapper.ToRuntimeStatus(deployment.Name, status);
    }

    public async Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(WorkloadDeployment deployment, int tail, CancellationToken ct = default)
    {
        if (deployment.AgentId is null || !channel.IsConnected(deployment.AgentId))
        {
            return [];
        }

        return await channel.GetLogsAsync(deployment.AgentId, deployment.Name, tail, ct);
    }

    public async Task SendConsoleAsync(WorkloadDeployment deployment, string text, CancellationToken ct = default)
    {
        var agentId = RequireAgent(deployment);
        var status = await channel.SendConsoleAsync(agentId, deployment.Name, text, ct);

        if (!status.Running)
        {
            throw new ConflictException(status.Detail ?? $"'{deployment.Name}' is not running.");
        }
    }

    public async Task UpdateAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var agentId = RequireAgent(deployment);
        await channel.UpdateAsync(agentId, AgentWorkloadSpecMapper.ToSpec(deployment), ct);
    }

    private static string RequireAgent(WorkloadDeployment deployment)
        => string.IsNullOrEmpty(deployment.AgentId)
            ? throw new ValidationException(nameof(WorkloadDeployment.AgentId), "This workload has no agent assigned.")
            : deployment.AgentId;
}
