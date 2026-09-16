using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

/// split from INodeSource since agent nodes have nothing to set
public interface INodeControl
{
    Task SetAvailabilityAsync(string nodeId, NodeAvailability availability, CancellationToken ct = default);

    Task SetRoleAsync(string nodeId, NodeRole role, CancellationToken ct = default);

    Task RemoveAsync(string nodeId, CancellationToken ct = default);
}
