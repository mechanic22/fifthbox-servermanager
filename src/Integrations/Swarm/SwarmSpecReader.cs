using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Reads a live swarm service into the shape the drift comparison speaks. Pure — the tested half of
/// reading a service back.
public static class SwarmSpecReader
{
    public static DeployedSpec Read(SwarmService service, IEnumerable<TaskResponse>? tasks = null)
    {
        var container = service.Spec?.TaskTemplate?.ContainerSpec;

        return new DeployedSpec
        {
            Image = container?.Image ?? string.Empty,
            Replicas = (int)(service.Spec?.Mode?.Replicated?.Replicas ?? 0),
            Env = container?.Env is { } env ? [.. env] : [],

            // A port with no published port is scheduler-assigned and not something the workload asked
            // for, so it can't be compared against a mapping the operator wrote.
            Ports = service.Spec?.EndpointSpec?.Ports is { } ports ? Keys(ports) : [],
            RunningPorts = RunningPorts(tasks),
        };
    }

    /// What the containers that are actually up are bound to. A host-mode port is published by the
    /// container, so this is the only place that knows whether a port change ever reached the host —
    /// the service spec says what swarm was told, not what is listening.
    private static IReadOnlyList<string> RunningPorts(IEnumerable<TaskResponse>? tasks)
        => tasks is null
            ? []
            : [.. Keys(tasks
                    .Where(t => t.Status?.State == TaskState.Running && t.Status.PortStatus?.Ports is not null)
                    .SelectMany(t => t.Status.PortStatus.Ports))
                .Distinct(StringComparer.Ordinal)];

    private static List<string> Keys(IEnumerable<PortConfig> ports) =>
    [
        .. ports
            .Where(p => p.PublishedPort > 0)
            .Select(p => $"{p.PublishedPort}:{p.TargetPort}/{p.Protocol ?? "tcp"}")
    ];
}
