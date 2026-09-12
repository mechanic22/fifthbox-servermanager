using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.App.Access;

public interface IWorkloadAccess
{
    /// One caller's resolved access, ready to be asked about any group or workload.
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

        // Their own grants and their teams' arrive as one flat list; AccessMap already keeps the
        // highest level per target, so holding both a personal and a team grant needs no special case.
        var held = await grants.ListForSubjectsAsync(subjects, ct);

        // Nobody with no grants needs the group tree loaded to be told no.
        return held.Count == 0
            ? AccessMap.Build([], [])
            : AccessMap.Build(held, await groups.ListAsync(ct));
    }
}
