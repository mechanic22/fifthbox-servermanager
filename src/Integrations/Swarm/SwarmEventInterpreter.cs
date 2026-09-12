using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Workloads;
using Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Turns a raw docker event into the subject it affects, or nothing. The tested core of the event source.
public static class SwarmEventInterpreter
{
    /// Service events name the service plainly; container events carry it as a swarm label. Two keys for
    /// the same fact because one is a swarm-scope object and the other is a local container.
    private const string ServiceNameAttribute = "name";
    private const string SwarmServiceNameAttribute = "com.docker.swarm.service.name";

    public static SwarmChange? Interpret(Message message)
    {
        if (message.Type == "node")
        {
            return new SwarmChange(SwarmChangeKind.Node, null);
        }

        var serviceName = message.Type switch
        {
            "service" => Attribute(message, ServiceNameAttribute),
            "container" => Attribute(message, SwarmServiceNameAttribute),
            _ => null,
        };

        // Unprefixed means it isn't ours: the platform's own fbsm-nginx / fbsm-host (single dash), or
        // anything else sharing the daemon.
        return SwarmNaming.TryWorkloadName(serviceName, out _)
            ? new SwarmChange(SwarmChangeKind.Workload, serviceName)
            : null;
    }

    private static string? Attribute(Message message, string key)
        => message.Actor?.Attributes is { } attributes && attributes.TryGetValue(key, out var value)
            ? value
            : null;
}
