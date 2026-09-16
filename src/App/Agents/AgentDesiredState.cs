using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

public interface IAgentDesiredState
{
    /// asked on agent connect so either side can reconcile after a restart
    Task<IReadOnlyList<AgentWorkloadSpec>> ForAgentAsync(string agentId, CancellationToken ct = default);
}

public sealed class AgentDesiredState(IWorkloadRepository workloads, WorkloadDeploymentFactory deployments) : IAgentDesiredState
{
    public async Task<IReadOnlyList<AgentWorkloadSpec>> ForAgentAsync(string agentId, CancellationToken ct = default)
    {
        var all = await workloads.ListAsync(ct);

        return all
            .Where(w => w.Kind == WorkloadKind.Native && w.AgentId == agentId)
            // stopped is desired state too, otherwise a reconnect starts it again
            .Where(w => w.DesiredState != WorkloadDesiredState.Stopped)
            // never-deployed is only saved, don't start it
            .Select(w => (Workload: w, Running: Newest(w)))
            .Where(x => x.Running is not null)
            // running revision, not saved config, so unsaved edits don't sneak out
            .Select(x => AgentWorkloadSpecMapper.ToSpec(deployments.ToDeployment(x.Workload, x.Running!)))
            .ToList();
    }

    /// the applied revision, not just the newest
    private static WorkloadRevision? Newest(Workload w)
        => w.Revisions.Where(r => r.Applied).OrderByDescending(r => r.Number).FirstOrDefault();
}
