using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// The spine of the PaaS: run a workload on a backend. Each backend handles one kind (SwarmBackend →
/// Container, AgentBackend → Native); the resolver picks the right one from a workload's kind. All ops
/// take the full deployment so the backend has its target (service name, or agent id).
public interface IWorkloadBackend
{
    WorkloadKind SupportedKind { get; }

    Task DeployAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    Task ScaleAsync(WorkloadDeployment deployment, int replicas, CancellationToken ct = default);

    /// Bounce the running instance on its current config — no config change. Swarm force-updates tasks;
    /// an agent stops then re-starts the process.
    Task RestartAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    /// Stop running it, but keep it deployed — swarm holds the service at zero replicas so its ports,
    /// config objects and task history survive and starting it again is a scale rather than a create.
    Task StopAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// The inverse of StopAsync: put a stopped workload back to work on the config it was last running.
    /// Swarm scales the service back up, or recreates it where Stop had to remove it outright.
    Task StartAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// Take it off the backend entirely. Idempotent: nothing there is a successful undeploy.
    Task UndeployAsync(WorkloadDeployment deployment, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> GetStatusAsync(WorkloadDeployment deployment, CancellationToken ct = default);

    /// The most recent output lines, oldest first. Empty rather than throwing when there is nothing to
    /// read — a workload that was never deployed has no logs, and that isn't an error.
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(WorkloadDeployment deployment, int tail, CancellationToken ct = default);

    /// Send a line to the running workload's console. Only backends that keep a process attached to a
    /// pipe can do this; the rest refuse rather than pretending.
    Task SendConsoleAsync(WorkloadDeployment deployment, string text, CancellationToken ct = default);

    /// Acquire the workload's files. Returns once the work has started, not once it has finished — an
    /// acquire can run for many minutes and nothing may block on it.
    Task UpdateAsync(WorkloadDeployment deployment, CancellationToken ct = default);
}

/// Picks the backend for a workload's kind.
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
