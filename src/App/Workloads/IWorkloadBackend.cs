using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public interface IWorkloadBackend
{
    WorkloadKind SupportedKind { get; }

    Task DeployAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    Task ScaleAsync(WorkloadDeployment deployment, int replicas, CancellationToken ct = default);

    /// no config change, swarm force-updates, an agent stops then starts
    Task RestartAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    /// stays deployed, swarm sits at zero replicas so ports, configs and history survive
    Task StopAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// back on the last running config, swarm scales up or recreates if Stop removed it
    Task StartAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// idempotent, nothing there counts as success
    Task UndeployAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> GetStatusAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// oldest first, empty (not a throw) when there's nothing to read
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(WorkloadDeployment deployment, int tail, CancellationToken ct = default);

    /// only backends with an attached process can, the rest refuse
    Task SendConsoleAsync(WorkloadDeployment deployment, string text, CancellationToken ct = default);

    /// returns once started, an acquire can take many minutes
    Task UpdateAsync(WorkloadDeployment deployment, CancellationToken ct = default);
}

public interface IWorkloadBackendResolver
{
    IWorkloadBackend Resolve(WorkloadKind kind);
}

public sealed class WorkloadBackendResolver(IEnumerable<IWorkloadBackend> backends) : IWorkloadBackendResolver
{
    public IWorkloadBackend Resolve(WorkloadKind kind)
        => backends.FirstOrDefault(b => b.SupportedKind == kind)
           ?? throw new InvalidOperationException($"No backend registered for workload kind '{kind}'.");
}
