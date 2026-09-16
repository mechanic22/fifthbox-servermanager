using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

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

            // no published port = scheduler-assigned, nothing the operator asked for
            Ports = service.Spec?.EndpointSpec?.Ports is { } ports ? Keys(ports) : [],
            RunningPorts = RunningPorts(tasks),
        };
    }

    /// what running containers are actually bound to, the spec only says what swarm was told
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
