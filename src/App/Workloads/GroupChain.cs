namespace FifthBox.ServerManager.App.Workloads;

public static class GroupChain
{
    /// nearest first, stops on a missing parent or a repeat since ParentId has no cycle check
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
