using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Components;

/// Flattens the group forest into a depth-annotated, hierarchy-ordered list for dropdowns.
public static class GroupHierarchy
{
    public static List<(WorkloadGroupResponse Group, int Depth)> Flatten(IReadOnlyList<WorkloadGroupResponse> groups)
    {
        var byParent = groups
            .GroupBy(g => g.ParentId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

        var result = new List<(WorkloadGroupResponse, int)>();

        void Walk(string parentKey, int depth)
        {
            if (!byParent.TryGetValue(parentKey, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                result.Add((child, depth));
                Walk(child.Id, depth + 1);
            }
        }

        Walk(string.Empty, 0);
        return result;
    }

    /// Non-breaking-space indent so nesting reads in a plain <option>.
    public static string Indent(int depth) => depth == 0 ? string.Empty : new string(' ', depth * 4);
}
