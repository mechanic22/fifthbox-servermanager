using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// catches someone running docker service update by hand
public static class SpecDrift
{
    public static IReadOnlyList<string> Compare(WorkloadDeployment expected, DeployedSpec live)
    {
        var drifted = new List<string>();

        if (!SameImage(expected.Image, live.Image))
        {
            drifted.Add("image");
        }

        if (expected.Replicas != live.Replicas)
        {
            drifted.Add("replicas");
        }

        if (!SameSet(expected.Env.Select(e => $"{e.Key}={e.Value}"), live.Env))
        {
            drifted.Add("environment");
        }

        var ports = expected.Ports.SelectMany(PortKeys).ToList();

        // both, a spec mismatch means someone edited the service, a container mismatch means ours never landed
        // running ports only compared when the backend reports them
        if (!SameSet(ports, live.Ports)
            || (live.RunningPorts.Count > 0 && !SameSet(ports, live.RunningPorts)))
        {
            drifted.Add("ports");
        }

        return drifted;
    }

    /// swarm pins the digest ("nginx:1.27@sha256:…") and tags untagged images "latest"
    private static bool SameImage(string expected, string live)
        => string.Equals(Normalize(expected), Normalize(live), StringComparison.Ordinal);

    private static string Normalize(string image)
    {
        var withoutDigest = image.Split('@', 2)[0];

        // a colon in the last segment is a tag, earlier it's a registry port
        var lastSegment = withoutDigest[(withoutDigest.LastIndexOf('/') + 1)..];
        return lastSegment.Contains(':') ? withoutDigest : $"{withoutDigest}:latest";
    }

    /// one entry per protocol, same as how Both gets published
    private static IEnumerable<string> PortKeys(PortMapping p)
    {
        var protocols = p.Protocol switch
        {
            PortProtocol.Both => new[] { "tcp", "udp" },
            PortProtocol.Udp => ["udp"],
            _ => new[] { "tcp" },
        };

        return protocols.Select(proto => $"{p.Published}:{p.Target}/{proto}");
    }

    private static bool SameSet(IEnumerable<string> expected, IEnumerable<string> live)
        => expected.ToHashSet(StringComparer.Ordinal).SetEquals(live);
}
