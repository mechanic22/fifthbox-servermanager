using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

public interface IAgentDesiredState
{
    /// What an agent should be running right now. Answered when an agent connects so it can reconcile
    /// after either side restarted.
    Task<IReadOnlyList<AgentWorkloadSpec>> ForAgentAsync(string agentId, CancellationToken ct = default);
}

public sealed class AgentDesiredState(IWorkloadRepository workloads, WorkloadDeploymentFactory deployments) : IAgentDesiredState
{
    public async Task<IReadOnlyList<AgentWorkloadSpec>> ForAgentAsync(string agentId, CancellationToken ct = default)
    {
        var all = await workloads.ListAsync(ct);

        return all
            .Where(w => w.Kind == WorkloadKind.Native && w.AgentId == agentId)
            // A stopped workload is desired state too — the desire is that it isn't running. Without this
            // an agent reconnect starts it again and Stop silently doesn't survive a restart.
            .Where(w => w.DesiredState != WorkloadDesiredState.Stopped)
            // Never-deployed workloads are definitions, not desired state — reconciling on them would
            // start something the operator only saved.
            .Select(w => (Workload: w, Running: Newest(w)))
            .Where(x => x.Running is not null)
            // The running revision, not the saved config: reconcile must not quietly apply unsaved edits.
            .Select(x => AgentWorkloadSpecMapper.ToSpec(deployments.ToDeployment(x.Workload, x.Running!)))
            .ToList();
    }

    /// The applied revision, not merely the newest — reconcile must restart what was actually running.
    private static WorkloadRevision? Newest(Workload w)
        => w.Revisions.Where(r => r.Applied).OrderByDescending(r => r.Number).FirstOrDefault();
}
