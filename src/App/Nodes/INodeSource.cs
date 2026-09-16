namespace FifthBox.ServerManager.App.Nodes;

public interface INodeSource
{
    Task<IReadOnlyList<Node>> GetNodesAsync(CancellationToken ct = default);
}
