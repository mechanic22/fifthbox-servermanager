using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Teams;

namespace FifthBox.ServerManager.App.Access;

public interface ITeamService
{
    Task<IReadOnlyList<TeamResponse>> ListAsync(Caller caller, CancellationToken ct = default);
    Task<TeamResponse> CreateAsync(Caller caller, SaveTeamRequest request, CancellationToken ct = default);
    Task<TeamResponse> UpdateAsync(Caller caller, string id, SaveTeamRequest request, CancellationToken ct = default);
    Task<TeamResponse> SetMembersAsync(Caller caller, string id, SetTeamMembersRequest request, CancellationToken ct = default);

    /// Deletes the team and every grant it held. Members lose that access; anything granted to them
    /// personally is untouched.
    Task DeleteAsync(Caller caller, string id, CancellationToken ct = default);
}

public sealed class TeamService(
    ITeamRepository teams,
    IUserDirectory users,
    IAccessGrantRepository grants,
    TimeProvider clock) : ITeamService
{
    public async Task<IReadOnlyList<TeamResponse>> ListAsync(Caller caller, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var all = await teams.ListAsync(ct);
        if (all.Count == 0)
        {
            return [];
        }

        var directory = await users.ListAsync(ct);
        var counts = await GrantCountsAsync(ct);
        return [.. all.Select(t => Map(t, directory, counts.GetValueOrDefault(t.Id)))];
    }

    public async Task<TeamResponse> CreateAsync(Caller caller, SaveTeamRequest request, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var name = ValidateName(request.Name);
        await RequireNameFreeAsync(name, excludingId: null, ct);

        var now = clock.GetUtcNow();
        var team = new Team
        {
            Name = name,
            Description = (request.Description ?? string.Empty).Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await teams.AddAsync(team, ct);

        return Map(team, await users.ListAsync(ct), 0);
    }

    public async Task<TeamResponse> UpdateAsync(Caller caller, string id, SaveTeamRequest request, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var team = await FindAsync(id, ct);
        var name = ValidateName(request.Name);
        await RequireNameFreeAsync(name, excludingId: id, ct);

        team.Name = name;
        team.Description = (request.Description ?? string.Empty).Trim();
        team.UpdatedAt = clock.GetUtcNow();
        await teams.UpdateAsync(team, ct);

        var counts = await GrantCountsAsync(ct);
        return Map(team, await users.ListAsync(ct), counts.GetValueOrDefault(team.Id));
    }

    public async Task<TeamResponse> SetMembersAsync(Caller caller, string id, SetTeamMembersRequest request, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var team = await FindAsync(id, ct);
        var directory = await users.ListAsync(ct);
        var known = directory.ToDictionary(u => u.Id, StringComparer.Ordinal);

        var wanted = (request.UserIds ?? [])
            .Select(u => (u ?? string.Empty).Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unknown = wanted.Where(u => !known.ContainsKey(u)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(nameof(SetTeamMembersRequest.UserIds), $"User not found: {string.Join(", ", unknown)}.");
        }

        team.MemberIds = wanted;
        team.UpdatedAt = clock.GetUtcNow();
        await teams.UpdateAsync(team, ct);

        var counts = await GrantCountsAsync(ct);
        return Map(team, directory, counts.GetValueOrDefault(team.Id));
    }

    public async Task DeleteAsync(Caller caller, string id, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var team = await FindAsync(id, ct);
        await teams.RemoveAsync(team, ct);
        await grants.RemoveForSubjectAsync(GrantSubject.Team(team.Id), ct);
    }

    private async Task<Dictionary<string, int>> GrantCountsAsync(CancellationToken ct)
        => (await grants.ListAsync(ct))
            .Where(g => g.SubjectType == AccessSubject.Team)
            .GroupBy(g => g.SubjectId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    private static TeamResponse Map(Team team, IReadOnlyList<DirectoryUser> directory, int grantCount)
    {
        var byId = directory.ToDictionary(u => u.Id, StringComparer.Ordinal);
        return new TeamResponse
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            GrantCount = grantCount,
            Members =
            [
                .. team.MemberIds
                    .Select(byId.GetValueOrDefault)
                    .OfType<DirectoryUser>()
                    .Select(u => new TeamMemberResponse { Id = u.Id, UserName = u.UserName, IsAdmin = u.IsAdmin })
                    .OrderBy(m => m.UserName, StringComparer.OrdinalIgnoreCase)
            ],
        };
    }

    private async Task<Team> FindAsync(string id, CancellationToken ct)
        => await teams.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Team '{id}' not found.");

    private static string ValidateName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException(nameof(SaveTeamRequest.Name), "Name is required.");
        }

        if (trimmed.Length > 100)
        {
            throw new ValidationException(nameof(SaveTeamRequest.Name), "Name must be 100 characters or fewer.");
        }

        return trimmed;
    }

    private async Task RequireNameFreeAsync(string name, string? excludingId, CancellationToken ct)
    {
        var clash = (await teams.ListAsync(ct))
            .Any(t => t.Id != excludingId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        if (clash)
        {
            throw new ConflictException($"A team named '{name}' already exists.");
        }
    }

    private static void RequireAdmin(Caller caller)
    {
        if (!caller.IsAdmin)
        {
            throw new ForbiddenException("Only an administrator can do that.");
        }
    }
}
