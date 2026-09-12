using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Nodes;

/// Compares two node snapshots for meaningful change, order-independent. The refresh uses this to
/// publish only when something actually changed.
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
