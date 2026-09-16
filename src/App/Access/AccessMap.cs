using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

/// group grants flow down the subtree and max with direct ones, a grant never lowers access
public sealed class AccessMap
{
    private readonly Dictionary<string, AccessLevel> _groups;
    private readonly Dictionary<string, AccessLevel> _workloads;
    private readonly bool _unrestricted;

    private AccessMap(Dictionary<string, AccessLevel> groups, Dictionary<string, AccessLevel> workloads, bool unrestricted)
    {
        _groups = groups;
        _workloads = workloads;
        _unrestricted = unrestricted;
    }

    /// admins hold no grants, this is where the role turns into a level
    public static AccessMap Admin { get; } = new(
        new Dictionary<string, AccessLevel>(StringComparer.Ordinal),
        new Dictionary<string, AccessLevel>(StringComparer.Ordinal),
        unrestricted: true);

    public static AccessMap Build(IReadOnlyList<AccessGrant> grantsForOneUser, IReadOnlyList<WorkloadGroup> groups)
    {
        var byId = groups.ToDictionary(g => g.Id, StringComparer.Ordinal);

        var granted = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        var workloads = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        foreach (var grant in grantsForOneUser)
        {
            var target = grant.Scope == AccessScope.Group ? granted : workloads;
            target[grant.TargetId] = Max(target.GetValueOrDefault(grant.TargetId), grant.Level);
        }

        var resolved = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var level = AccessLevel.None;
            foreach (var id in GroupChain.SelfAndAncestors(group.Id, byId))
            {
                level = Max(level, granted.GetValueOrDefault(id));
            }

            if (level > AccessLevel.None)
            {
                resolved[group.Id] = level;
            }
        }

        return new AccessMap(resolved, workloads, unrestricted: false);
    }

    public AccessLevel ForGroup(string? groupId)
        => _unrestricted ? AccessLevel.Configure
        : groupId is null ? AccessLevel.None
        : _groups.GetValueOrDefault(groupId);

    public AccessLevel ForWorkload(string workloadId, string? groupId)
        => _unrestricted ? AccessLevel.Configure
        : Max(_workloads.GetValueOrDefault(workloadId), ForGroup(groupId));

    private static AccessLevel Max(AccessLevel a, AccessLevel b) => a >= b ? a : b;
}
