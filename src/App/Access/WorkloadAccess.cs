using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.App.Access;

public interface IWorkloadAccess
{
    Task<AccessMap> MapAsync(Caller caller, CancellationToken ct = default);
}

public sealed class WorkloadAccess(
    IAccessGrantRepository grants,
    IWorkloadGroupRepository groups,
    ITeamRepository teams) : IWorkloadAccess
{
    public async Task<AccessMap> MapAsync(Caller caller, CancellationToken ct = default)
    {
        if (caller.IsAdmin)
        {
            return AccessMap.Admin;
        }

        var memberships = await teams.ListForUserAsync(caller.UserId, ct);
        List<GrantSubject> subjects =
            [GrantSubject.User(caller.UserId), .. memberships.Select(t => GrantSubject.Team(t.Id))];

        // personal and team grants come back flat, AccessMap keeps the max per target
        var held = await grants.ListForSubjectsAsync(subjects, ct);

        // no grants, no need to load the group tree
        return held.Count == 0
            ? AccessMap.Build([], [])
            : AccessMap.Build(held, await groups.ListAsync(ct));
    }
}
