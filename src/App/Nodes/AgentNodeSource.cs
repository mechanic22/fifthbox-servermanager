using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

public sealed class AgentNodeSource(IAgentRepository agents, IAgentRegistry registry) : INodeSource
{
    public async Task<IReadOnlyList<Node>> GetNodesAsync(CancellationToken ct = default)
    {
        var list = await agents.ListAsync(ct);
        return list.Select(a => new Node
        {
            Id = a.Id,
            Hostname = a.Name,
            Role = NodeRole.Worker,
            Status = registry.IsOnline(a.Id) ? NodeStatus.Ready : NodeStatus.Down,
            Availability = NodeAvailability.Active,
            Platform = MapPlatform(a.Platform),
            Backend = NodeBackendKind.Agent,
            LastSeenAt = a.LastSeenAt,
        }).ToList();
    }

    private static NodePlatform MapPlatform(AgentPlatform platform) => platform switch
    {
        AgentPlatform.Windows => NodePlatform.Windows,
        AgentPlatform.Linux => NodePlatform.Linux,
        AgentPlatform.MacOS => NodePlatform.MacOS,
        _ => NodePlatform.Unknown,
    };
}
