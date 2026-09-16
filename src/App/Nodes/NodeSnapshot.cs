using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

/// order-independent
public static class NodeSnapshot
{
    public static bool Differs(IReadOnlyList<NodeResponse> previous, IReadOnlyList<NodeResponse> current)
    {
        if (previous.Count != current.Count)
        {
            return true;
        }

        var a = previous.OrderBy(n => n.Id, StringComparer.Ordinal);
        var b = current.OrderBy(n => n.Id, StringComparer.Ordinal);
        return !a.SequenceEqual(b);
    }
}
