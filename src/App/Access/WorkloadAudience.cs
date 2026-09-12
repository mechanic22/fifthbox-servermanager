using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Access;

/// Who may hear about one workload. Separate from <see cref="IWorkloadAccess"/> because this asks about
/// a workload rather than about a caller — it's what the hub and the status broadcaster need.
public interface IWorkloadAudience
{
    /// Throws unless the caller can see this workload, without loading it for them.
    Task RequireViewAsync(Caller caller, string workloadId, CancellationToken ct = default);

    /// Every admin, plus anyone whose grant — their own or one of their teams' — reaches this workload
    /// directly or through a group.
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

        // Resolved per user rather than matched on TargetId: most viewers hold no grant on the workload
        // itself, only on a group somewhere above it — or on neither, through a team.
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
