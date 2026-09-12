using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

public interface INodeService
{
    Task<IReadOnlyList<NodeResponse>> ListAsync(CancellationToken ct = default);
    Task<NodeResponse> GetAsync(string id, CancellationToken ct = default);

    /// Re-read every source and update the shared state. Answers whether anything actually changed, so
    /// the caller can decide whether connected clients need telling.
    Task<bool> RefreshAsync(CancellationToken ct = default);

    /// Whether the scheduler may place work here. Drain also moves what's already running off.
    Task<NodeResponse> SetAvailabilityAsync(string id, NodeAvailability availability, CancellationToken ct = default);

    Task<NodeResponse> SetRoleAsync(string id, NodeRole role, CancellationToken ct = default);

    /// Drop a node out of the swarm. Only a node that's already down — evicting a live one leaves it
    /// believing it's still a member.
    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed class NodeService(
    IEnumerable<INodeSource> sources,
    IClusterState state,
    INodeControl control,
    ISwarmLifecycle swarm) : INodeService
{
    public async Task<IReadOnlyList<NodeResponse>> ListAsync(CancellationToken ct = default)
    {
        // Cold start: nothing has observed the cluster yet, so pay for one fan-out rather than answering
        // from an empty store. Every later read is memory.
        if (!state.Hydrated)
        {
            await RefreshAsync(ct);
        }

        return state.Nodes;
    }

    public async Task<NodeResponse> GetAsync(string id, CancellationToken ct = default)
        => (await ListAsync(ct)).FirstOrDefault(n => n.Id == id)
           ?? throw new NotFoundException($"Node '{id}' not found.");

    public async Task<bool> RefreshAsync(CancellationToken ct = default)
        => state.SetNodes([.. (await AllNodesAsync(ct)).Select(ToResponse)]);

    public async Task<NodeResponse> SetAvailabilityAsync(string id, NodeAvailability availability, CancellationToken ct = default)
    {
        var node = await SwarmNodeAsync(id, ct);
        if (availability == NodeAvailability.Unknown)
        {
            throw new ValidationException(nameof(availability), "Pick active, pause or drain.");
        }

        await control.SetAvailabilityAsync(node.Id, availability, ct);
        return await RefreshedAsync(id, ct);
    }

    public async Task<NodeResponse> SetRoleAsync(string id, NodeRole role, CancellationToken ct = default)
    {
        var node = await SwarmNodeAsync(id, ct);
        if (node.Role == role)
        {
            return node;
        }

        if (role == NodeRole.Worker)
        {
            await GuardDemotionAsync(node, ct);
        }

        await control.SetRoleAsync(node.Id, role, ct);
        return await RefreshedAsync(id, ct);
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        var node = await SwarmNodeAsync(id, ct);
        if (node.Status != NodeStatus.Down)
        {
            throw new ConflictException(
                $"'{node.Hostname}' is still reachable. Shut its Docker engine down first, or have it leave the swarm — "
                + "evicting a live node leaves it believing it is still a member.");
        }

        if (node.Id == await LocalNodeIdAsync(ct))
        {
            throw new ConflictException("This is the node ServerManager runs on.");
        }

        await control.RemoveAsync(node.Id, ct);
        await RefreshAsync(ct);
    }

    private async Task GuardDemotionAsync(NodeResponse node, CancellationToken ct)
    {
        // Demoting the node whose Docker socket we hold takes the manager API away from ServerManager
        // itself — the cluster keeps running and nothing here can touch it again.
        if (node.Id == await LocalNodeIdAsync(ct))
        {
            throw new ConflictException(
                "ServerManager reaches Docker through this node. Demoting it would leave it unable to manage the cluster.");
        }

        var managers = (await ListAsync(ct)).Count(n => n.Backend == NodeBackendKind.Swarm && n.Role == NodeRole.Manager);
        if (managers <= 1)
        {
            throw new ConflictException("A swarm needs at least one manager.");
        }
    }

    private async Task<string?> LocalNodeIdAsync(CancellationToken ct)
    {
        try
        {
            return (await swarm.GetStateAsync(ct)).NodeId;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Can't tell which node is ours, so the self-protection guards can't fire. Better to let the
            // operator through than to block every node operation because one read failed.
            return null;
        }
    }

    /// Node operations are only meaningful on a swarm node — an agent has no schedulability, no role,
    /// and is removed from the Agents list instead.
    private async Task<NodeResponse> SwarmNodeAsync(string id, CancellationToken ct)
    {
        var node = await GetAsync(id, ct);
        return node.Backend == NodeBackendKind.Swarm
            ? node
            : throw new ConflictException($"'{node.Hostname}' is an agent, not a swarm node.");
    }

    private async Task<NodeResponse> RefreshedAsync(string id, CancellationToken ct)
    {
        await RefreshAsync(ct);
        return await GetAsync(id, ct);
    }

    // Every backend that surfaces nodes (the swarm + the agent fleet) contributes to one combined list.
    // A source that can't answer contributes nothing rather than emptying the list: before the cluster is
    // bootstrapped the swarm source always fails, and that must not hide the agents, which don't need one.
    // Whether the swarm itself is reachable is the Cluster page's job to report, not this list's.
    private async Task<IReadOnlyList<Node>> AllNodesAsync(CancellationToken ct)
    {
        var all = new List<Node>();
        foreach (var source in sources)
        {
            try
            {
                all.AddRange(await source.GetNodesAsync(ct));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                continue;
            }
        }

        return all;
    }

    private static NodeResponse ToResponse(Node n) => new()
    {
        Id = n.Id,
        Hostname = n.Hostname,
        Role = n.Role,
        Status = n.Status,
        Availability = n.Availability,
        Platform = n.Platform,
        Architecture = n.Architecture,
        IsLeader = n.IsLeader,
        EngineVersion = n.EngineVersion,
        Address = n.Address,
        Backend = n.Backend,
        LastSeenAt = n.LastSeenAt,
    };
}
