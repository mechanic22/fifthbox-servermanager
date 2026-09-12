using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Lists ServerManager's platform-labelled swarm services (nginx, later letsencrypt) and bounces them via
/// force-update. Thin Docker adapter — state derivation reuses WorkloadSpecMapper.
public sealed class SwarmPlatformServices(IDockerClient client) : IPlatformServices
{
    public async Task<IReadOnlyList<PlatformServiceResponse>> ListAsync(CancellationToken ct = default)
    {
        var all = await client.Swarm.ListServicesAsync(cancellationToken: ct);
        var services = all
            .Where(s => s.Spec?.Labels is { } labels
                && labels.TryGetValue(PlatformLabels.RoleKey, out var role) && role == PlatformLabels.PlatformRole)
            .ToList();

        var result = new List<PlatformServiceResponse>(services.Count);
        foreach (var s in services)
        {
            var name = s.Spec?.Name ?? s.ID;
            var desired = (int)(s.Spec?.Mode?.Replicated?.Replicas ?? 0);
            var tasks = await client.Tasks.ListAsync(new TasksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>> { ["service"] = new Dictionary<string, bool> { [s.ID] = true } },
            }, ct);
            var running = tasks.Count(t => t.Status?.State == TaskState.Running);

            result.Add(new PlatformServiceResponse
            {
                Name = name,
                Image = s.Spec?.TaskTemplate?.ContainerSpec?.Image,
                State = WorkloadSpecMapper.ToRuntimeStatus(name, deployed: true, desired, running).State,
                RunningReplicas = running,
                DesiredReplicas = desired,
            });
        }

        return result;
    }

    public async Task<bool> ServiceExistsAsync(string name, CancellationToken ct = default)
    {
        var services = await client.Swarm.ListServicesAsync(cancellationToken: ct);
        return services.Any(s => string.Equals(s.Spec?.Name, name, StringComparison.Ordinal));
    }

    public async Task<string?> LocalNodeIdAsync(CancellationToken ct = default)
        => (await client.System.GetSystemInfoAsync(ct)).Swarm?.NodeID;

    public async Task RestartAsync(string name, CancellationToken ct = default)
    {
        var services = await client.Swarm.ListServicesAsync(cancellationToken: ct);
        var svc = services.FirstOrDefault(s => string.Equals(s.Spec?.Name, name, StringComparison.Ordinal))
            ?? throw new NotFoundException($"Platform service '{name}' not found.");

        var inspected = await client.Swarm.InspectServiceAsync(svc.ID, ct);
        var spec = inspected.Spec;
        spec.TaskTemplate ??= new TaskSpec();
        spec.TaskTemplate.ForceUpdate += 1;

        await client.Swarm.UpdateServiceAsync(svc.ID, new ServiceUpdateParameters
        {
            Version = (long)inspected.Version.Index,
            Service = spec,
        }, ct);
    }
}
