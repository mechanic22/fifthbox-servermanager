using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// Compares the config we believe is deployed against what the backend is really holding. Anyone who
/// knows docker will eventually run `docker service update` by hand, and without this the platform keeps
/// reporting "in sync" while the Config tab describes something that isn't running.
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

        // Both, because they fail differently: the spec disagreeing means someone edited the service,
        // and the containers disagreeing means an edit of ours never reached them. Running ports are
        // only compared when the backend reports them at all.
        if (!SameSet(ports, live.Ports)
            || (live.RunningPorts.Count > 0 && !SameSet(ports, live.RunningPorts)))
        {
            drifted.Add("ports");
        }

        return drifted;
    }

    /// Swarm pins the tag it resolved, so a service created from "nginx:1.27" reads back as
    /// "nginx:1.27@sha256:…", and an untagged image comes back tagged "latest".
    private static bool SameImage(string expected, string live)
        => string.Equals(Normalize(expected), Normalize(live), StringComparison.Ordinal);

    private static string Normalize(string image)
    {
        var withoutDigest = image.Split('@', 2)[0];

        // A colon in the last segment is a tag; one earlier is a registry port.
        var lastSegment = withoutDigest[(withoutDigest.LastIndexOf('/') + 1)..];
        return lastSegment.Contains(':') ? withoutDigest : $"{withoutDigest}:latest";
    }

    /// One entry per protocol, matching how a Both mapping is published.
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
