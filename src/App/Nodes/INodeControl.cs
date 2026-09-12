using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

/// Port: changing a swarm node rather than reading one. Separate from INodeSource because reading is
/// something every backend does and writing is not — an agent node has no schedulability or role to set.
public interface INodeControl
{
    Task SetAvailabilityAsync(string nodeId, NodeAvailability availability, CancellationToken ct = default);

    Task SetRoleAsync(string nodeId, NodeRole role, CancellationToken ct = default);

    Task RemoveAsync(string nodeId, CancellationToken ct = default);
}
