using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Realtime;

public interface IAgentStatusRelay
{
    Task RelayAsync(string agentId, AgentWorkloadStatus status, CancellationToken ct = default);
    Task RelayLogsAsync(string agentId, string workloadName, IReadOnlyList<WorkloadLogLine> lines, CancellationToken ct = default);

    /// marks the agent's workloads offline so its last push doesn't look current
    Task RelayOfflineAsync(string agentId, CancellationToken ct = default);
}

public sealed class AgentStatusRelay(
    IWorkloadRepository workloads,
    IClusterState state,
    IEventPublisher publisher,
    IWorkloadLogFollower follower,
    ILogger<AgentStatusRelay> logger) : IAgentStatusRelay
{
    public async Task RelayLogsAsync(string agentId, string workloadName, IReadOnlyList<WorkloadLogLine> lines, CancellationToken ct = default)
    {
        if (await FindAsync(agentId, workloadName, ct) is { } workload)
        {
            await follower.PublishAsync(workload.Id, lines, ct);
        }
    }

    /// find by unique name then check the agent, keeps status reports off a table scan
    private async Task<Workload?> FindAsync(string agentId, string workloadName, CancellationToken ct)
    {
        var workload = await workloads.FindByNameAsync(workloadName, ct);
        return workload?.AgentId == agentId ? workload : null;
    }

    public async Task RelayOfflineAsync(string agentId, CancellationToken ct = default)
    {
        var all = await workloads.ListAsync(ct);

        foreach (var workload in all.Where(w => w.Kind == WorkloadKind.Native && w.AgentId == agentId))
        {
            var runtime = new WorkloadRuntimeStatus
            {
                Name = workload.Name,
                Deployed = false,
                State = WorkloadState.NotDeployed,
                Detail = "the agent is offline",
            };

            if (state.SetWorkloadStatus(workload.Id, runtime))
            {
                await publisher.PublishAsync(new WorkloadStatusChanged(workload.Id, runtime), ct);
            }
        }
    }

    public async Task RelayAsync(string agentId, AgentWorkloadStatus status, CancellationToken ct = default)
    {
        var workload = await FindAsync(agentId, status.Name, ct);

        if (workload is null)
        {
            logger.LogDebug("Agent {AgentId} reported unknown workload '{Workload}'", agentId, status.Name);
            return;
        }

        var runtime = AgentStatusMapper.ToRuntimeStatus(status.Name, status);
        if (state.SetWorkloadStatus(workload.Id, runtime))
        {
            await publisher.PublishAsync(new WorkloadStatusChanged(workload.Id, runtime), ct);
        }
    }
}
