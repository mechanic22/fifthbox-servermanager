using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Access;

/// asks about a workload not a caller, for the hub and status broadcaster
public interface IWorkloadAudience
{
    /// throws unless the caller can see it, without loading it
    Task RequireViewAsync(Caller caller, string workloadId, CancellationToken ct = default);

    /// admins plus anyone whose own or team grant reaches it, directly or via a group
    Task<IReadOnlyList<string>> ViewerIdsAsync(string workloadId, CancellationToken ct = default);
}

public sealed class WorkloadAudience(
    IWorkloadAccess access,
    IAccessGrantRepository grants,
    IWorkloadGroupRepository groups,
    IWorkloadRepository workloads,
    ITeamRepository teams,
    IUserDirectory users) : IWorkloadAudience
{
    public async Task RequireViewAsync(Caller caller, string workloadId, CancellationToken ct = default)
    {
        var workload = await workloads.FindByIdAsync(workloadId, ct)
            ?? throw new NotFoundException($"Workload '{workloadId}' not found.");

        if ((await access.MapAsync(caller, ct)).ForWorkload(workload.Id, workload.GroupId) < AccessLevel.View)
        {
            throw new NotFoundException($"Workload '{workloadId}' not found.");
        }
    }

    public async Task<IReadOnlyList<string>> ViewerIdsAsync(string workloadId, CancellationToken ct = default)
    {
        var everyone = await users.ListAsync(ct);
        var admins = everyone.Where(u => u.IsAdmin).Select(u => u.Id);

        var workload = await workloads.FindByIdAsync(workloadId, ct);
        if (workload is null)
        {
            return [.. admins];
        }

        var all = await grants.ListAsync(ct);
        var tree = await groups.ListAsync(ct);
        var rosters = await teams.ListAsync(ct);

        // per user, most viewers only hold a grant on a group above it, or get it through a team
        var granted = everyone
            .Where(u => !u.IsAdmin)
            .Where(u => AccessMap.Build(Held(u.Id, all, rosters), tree)
                .ForWorkload(workload.Id, workload.GroupId) >= AccessLevel.View)
            .Select(u => u.Id);

        return [.. admins.Concat(granted).Distinct(StringComparer.Ordinal)];
    }

    private static List<AccessGrant> Held(string userId, IReadOnlyList<AccessGrant> all, IReadOnlyList<Team> rosters)
    {
        var teamIds = rosters
            .Where(t => t.MemberIds.Contains(userId, StringComparer.Ordinal))
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. all.Where(g => g.SubjectType == AccessSubject.User
                ? string.Equals(g.SubjectId, userId, StringComparison.Ordinal)
                : teamIds.Contains(g.SubjectId))
        ];
    }
}
