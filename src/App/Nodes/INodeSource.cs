namespace FifthBox.ServerManager.App.Nodes;

/// Port: where the node inventory comes from. Implemented per backend — SwarmNodeSource today,
/// an agent-backed source later. The App depends on this, never on a concrete backend.
public interface INodeSource
{
    Task<IReadOnlyList<Node>> GetNodesAsync(CancellationToken ct = default);
}
