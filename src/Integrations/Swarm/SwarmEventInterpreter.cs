using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Workloads;
using Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm;

public static class SwarmEventInterpreter
{
    /// service events use "name", container events carry it as a swarm label
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

        // unprefixed isn't ours (fbsm-nginx, fbsm-host, or anything else on the daemon)
        return SwarmNaming.TryWorkloadName(serviceName, out _)
            ? new SwarmChange(SwarmChangeKind.Workload, serviceName)
            : null;
    }

    private static string? Attribute(Message message, string key)
        => message.Actor?.Attributes is { } attributes && attributes.TryGetValue(key, out var value)
            ? value
            : null;
}
