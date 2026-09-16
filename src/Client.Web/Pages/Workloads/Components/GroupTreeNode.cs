using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

public sealed class GroupTreeNode
{
    public required WorkloadGroupResponse Group { get; init; }
    public List<GroupTreeNode> Children { get; } = [];
    public List<WorkloadResponse> Workloads { get; } = [];

    /// groups with a missing parent become roots
    public static List<GroupTreeNode> Build(IReadOnlyList<WorkloadGroupResponse> groups, IReadOnlyList<WorkloadResponse> workloads)
    {
        var nodes = groups.ToDictionary(g => g.Id, g => new GroupTreeNode { Group = g });

        foreach (var w in workloads.Where(w => w.GroupId is not null && nodes.ContainsKey(w.GroupId!)))
        {
            nodes[w.GroupId!].Workloads.Add(w);
        }

        var roots = new List<GroupTreeNode>();
        foreach (var node in nodes.Values)
        {
            if (node.Group.ParentId is { } parentId && nodes.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
