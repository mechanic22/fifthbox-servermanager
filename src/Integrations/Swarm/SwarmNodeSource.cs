using Docker.DotNet;
using FifthBox.ServerManager.App.Nodes;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Thin adapter: reads swarm nodes from the Docker Engine API and hands each to SwarmNodeMapper.
/// No logic of its own, so it's test-exempt.
public sealed class SwarmNodeSource(IDockerClient client) : INodeSource
{
    public async Task<IReadOnlyList<Node>> GetNodesAsync(CancellationToken ct = default)
    {
        var nodes = await client.Swarm.ListNodesAsync(ct);
        return nodes.Select(SwarmNodeMapper.ToNode).ToList();
    }
}
