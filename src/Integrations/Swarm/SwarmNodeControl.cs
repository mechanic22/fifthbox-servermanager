using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Thin adapter for node updates. Every change is read-modify-write against the node's current spec at
/// its current version, because docker rejects an update carrying a stale one. Test-exempt; the rules
/// about which updates are allowed live in NodeService.
public sealed class SwarmNodeControl(IDockerClient client) : INodeControl
{
    public Task SetAvailabilityAsync(string nodeId, NodeAvailability availability, CancellationToken ct = default) =>
        UpdateAsync(nodeId, spec => spec.Availability = availability switch
        {
            NodeAvailability.Drain => "drain",
            NodeAvailability.Pause => "pause",
            _ => "active",
        }, ct);

    public Task SetRoleAsync(string nodeId, NodeRole role, CancellationToken ct = default) =>
        UpdateAsync(nodeId, spec => spec.Role = role == NodeRole.Manager ? "manager" : "worker", ct);

    public async Task RemoveAsync(string nodeId, CancellationToken ct = default) =>
        await client.Swarm.RemoveNodeAsync(nodeId, force: false, ct);

    private async Task UpdateAsync(string nodeId, Action<NodeUpdateParameters> change, CancellationToken ct)
    {
        var node = await client.Swarm.InspectNodeAsync(nodeId, ct);

        // Docker.DotNet types a node's Spec as NodeUpdateParameters, so the current spec is already the
        // shape the update wants — carry it over and change the one field, or the untouched half resets.
        var spec = node.Spec ?? new NodeUpdateParameters();
        change(spec);

        await client.Swarm.UpdateNodeAsync(nodeId, node.Version.Index, spec, ct);
    }
}
