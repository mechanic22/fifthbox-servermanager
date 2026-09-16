using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Access;

public interface IAccessService
{
    Task<IReadOnlyList<AccessGrantResponse>> ListAsync(CancellationToken ct = default);

    /// grants on it plus grants on any group above it
    Task<IReadOnlyList<AccessGrantResponse>> ListForTargetAsync(AccessScope scope, string targetId, CancellationToken ct = default);

    /// null when level None removed the grant
    Task<AccessGrantResponse?> SetAsync(SetAccessGrantRequest request, CancellationToken ct = default);

    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed class AccessService(
    IAccessGrantRepository grants,
    IUserDirectory users,
    ITeamRepository teams,
    IWorkloadRepository workloads,
    IWorkloadGroupRepository groups,
    TimeProvider clock) : IAccessService
{
    public async Task<IReadOnlyList<AccessGrantResponse>> ListAsync(CancellationToken ct = default)
    {
        var all = await grants.ListAsync(ct);
        if (all.Count == 0)
        {
            return [];
        }

        var userNames = (await users.ListAsync(ct)).ToDictionary(u => u.Id, u => u.UserName, StringComparer.Ordinal);
        var teamNames = (await teams.ListAsync(ct)).ToDictionary(t => t.Id, t => t.Name, StringComparer.Ordinal);
        var workloadNames = (await workloads.ListAsync(ct)).ToDictionary(w => w.Id, w => w.Name, StringComparer.Ordinal);
        var groupNames = (await groups.ListAsync(ct)).ToDictionary(g => g.Id, g => g.Name, StringComparer.Ordinal);

        return
        [
            .. all
                .Select(g => new AccessGrantResponse
                {
                    Id = g.Id,
                    SubjectType = g.SubjectType,
                    SubjectId = g.SubjectId,
                    SubjectName = (g.SubjectType == AccessSubject.User ? userNames : teamNames)
                        .GetValueOrDefault(g.SubjectId, string.Empty),
                    Scope = g.Scope,
                    TargetId = g.TargetId,
                    TargetName = (g.Scope == AccessScope.Group ? groupNames : workloadNames)
                        .GetValueOrDefault(g.TargetId, string.Empty),
                    Level = g.Level,
                })
                .OrderBy(g => g.SubjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.TargetName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<IReadOnlyList<AccessGrantResponse>> ListForTargetAsync(
        AccessScope scope, string targetId, CancellationToken ct = default)
    {
        var tree = await groups.ListAsync(ct);
        var byId = tree.ToDictionary(g => g.Id, StringComparer.Ordinal);

        string targetName;
        string? startGroupId;
        if (scope == AccessScope.Group)
        {
            var group = byId.GetValueOrDefault(targetId)
                ?? throw new NotFoundException($"Group '{targetId}' not found.");
            targetName = group.Name;
            startGroupId = group.ParentId;
        }
        else
        {
            var workload = await workloads.FindByIdAsync(targetId, ct)
                ?? throw new NotFoundException($"Workload '{targetId}' not found.");
            targetName = workload.Name;
            startGroupId = workload.GroupId;
        }

        var ancestors = GroupChain.SelfAndAncestors(startGroupId, byId).ToHashSet(StringComparer.Ordinal);

        var all = await grants.ListAsync(ct);
        var matching = all.Where(g =>
            (g.Scope == scope && string.Equals(g.TargetId, targetId, StringComparison.Ordinal))
            || (g.Scope == AccessScope.Group && ancestors.Contains(g.TargetId)));

        var userNames = (await users.ListAsync(ct)).ToDictionary(u => u.Id, u => u.UserName, StringComparer.Ordinal);
        var teamNames = (await teams.ListAsync(ct)).ToDictionary(t => t.Id, t => t.Name, StringComparer.Ordinal);

        return
        [
            .. matching
                .Select(g =>
                {
                    var direct = g.Scope == scope && string.Equals(g.TargetId, targetId, StringComparison.Ordinal);
                    return new AccessGrantResponse
                    {
                        Id = g.Id,
                        SubjectType = g.SubjectType,
                        SubjectId = g.SubjectId,
                        SubjectName = (g.SubjectType == AccessSubject.User ? userNames : teamNames)
                            .GetValueOrDefault(g.SubjectId, string.Empty),
                        Scope = g.Scope,
                        TargetId = g.TargetId,
                        TargetName = direct ? targetName : byId.GetValueOrDefault(g.TargetId)?.Name ?? string.Empty,
                        Level = g.Level,
                        Inherited = !direct,
                    };
                })
                .OrderBy(g => g.Inherited)
                .ThenBy(g => g.SubjectName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<AccessGrantResponse?> SetAsync(SetAccessGrantRequest request, CancellationToken ct = default)
    {
        var subject = new GrantSubject(request.SubjectType, request.SubjectId);
        var subjectName = await ResolveSubjectAsync(subject, ct);
        var targetName = await ResolveTargetAsync(request.Scope, request.TargetId, ct);
        var existing = await grants.FindAsync(subject, request.Scope, request.TargetId, ct);

        if (request.Level == AccessLevel.None)
        {
            if (existing is not null)
            {
                await grants.RemoveAsync(existing, ct);
            }

            return null;
        }

        var now = clock.GetUtcNow();
        if (existing is null)
        {
            existing = new AccessGrant
            {
                SubjectType = subject.Type,
                SubjectId = subject.Id,
                Scope = request.Scope,
                TargetId = request.TargetId,
                Level = request.Level,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await grants.AddAsync(existing, ct);
        }
        else
        {
            existing.Level = request.Level;
            existing.UpdatedAt = now;
            await grants.UpdateAsync(existing, ct);
        }

        return new AccessGrantResponse
        {
            Id = existing.Id,
            SubjectType = existing.SubjectType,
            SubjectId = existing.SubjectId,
            SubjectName = subjectName,
            Scope = existing.Scope,
            TargetId = existing.TargetId,
            TargetName = targetName,
            Level = existing.Level,
        };
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        var grant = await grants.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Grant '{id}' not found.");
        await grants.RemoveAsync(grant, ct);
    }

    private async Task<string> ResolveSubjectAsync(GrantSubject subject, CancellationToken ct)
    {
        if (subject.Type == AccessSubject.Team)
        {
            var team = await teams.FindByIdAsync(subject.Id, ct)
                ?? throw new ValidationException(nameof(SetAccessGrantRequest.SubjectId), "Team not found.");

            // no admin check unlike user grants, keeps the roster intact across a promotion
            return team.Name;
        }

        var user = await users.FindByIdAsync(subject.Id, ct)
            ?? throw new ValidationException(nameof(SetAccessGrantRequest.SubjectId), "User not found.");

        if (user.IsAdmin)
        {
            throw new ValidationException(
                nameof(SetAccessGrantRequest.SubjectId),
                $"{user.UserName} is an administrator and already has full access. Remove the admin role first.");
        }

        return user.UserName;
    }

    private async Task<string> ResolveTargetAsync(AccessScope scope, string targetId, CancellationToken ct)
    {
        if (scope == AccessScope.Group)
        {
            var group = await groups.FindByIdAsync(targetId, ct)
                ?? throw new ValidationException(nameof(SetAccessGrantRequest.TargetId), "Group not found.");
            return group.Name;
        }

        var workload = await workloads.FindByIdAsync(targetId, ct)
            ?? throw new ValidationException(nameof(SetAccessGrantRequest.TargetId), "Workload not found.");
        return workload.Name;
    }
}
