namespace FifthBox.ServerManager.App.Workloads;

public static class GroupChain
{
    /// The group, then each ancestor, nearest first. Stops on a missing parent or a repeat — ParentId
    /// has no cycle check anywhere, so a hand-edited row must not spin here.
    public static IEnumerable<string> SelfAndAncestors(string? groupId, IReadOnlyDictionary<string, WorkloadGroup> byId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = groupId;

        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            yield return current;
            current = byId.TryGetValue(current, out var group) ? group.ParentId : null;
        }
    }
}
