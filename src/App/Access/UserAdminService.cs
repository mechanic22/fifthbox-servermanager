using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Users;
using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Access;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserSummaryResponse>> ListAsync(Caller caller, CancellationToken ct = default);
    Task SetRolesAsync(Caller caller, string userId, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task DeleteAsync(Caller caller, string userId, CancellationToken ct = default);
}

public sealed class UserAdminService(IUserDirectory users, IAccessGrantRepository grants) : IUserAdminService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> ListAsync(Caller caller, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var all = await users.ListAsync(ct);
        var counts = (await grants.ListAsync(ct))
            .Where(g => g.SubjectType == AccessSubject.User)
            .GroupBy(g => g.SubjectId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return [.. all.Select(u => new UserSummaryResponse
        {
            Id = u.Id,
            UserName = u.UserName,
            Roles = [.. u.Roles],
            GrantCount = u.IsAdmin ? 0 : counts.GetValueOrDefault(u.Id),
        })];
    }

    public async Task SetRolesAsync(Caller caller, string userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var user = await FindAsync(userId, ct);
        var wanted = Normalize(roles);

        // Changing your own roles takes effect on the next request and there may be nobody left who can undo it.
        if (string.Equals(user.Id, caller.UserId, StringComparison.Ordinal))
        {
            throw new ConflictException("You can't change your own roles.");
        }

        var becomesAdmin = wanted.Contains(RoleNames.Admin);
        if (user.IsAdmin && !becomesAdmin)
        {
            await RequireAnotherAdminAsync(user.Id, ct);
        }

        if (user.Roles.OrderBy(r => r, StringComparer.Ordinal).SequenceEqual(wanted.OrderBy(r => r, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return;
        }

        await users.SetRolesAsync(user.Id, wanted, ct);

        // Admin makes any grants they held redundant — the role bypasses them, and leaving the rows behind
        // would silently restore that access on a later demotion.
        if (becomesAdmin)
        {
            await grants.RemoveForSubjectAsync(GrantSubject.User(user.Id), ct);
        }
    }

    public async Task DeleteAsync(Caller caller, string userId, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var user = await FindAsync(userId, ct);

        if (string.Equals(user.Id, caller.UserId, StringComparison.Ordinal))
        {
            throw new ConflictException("You can't delete your own account.");
        }

        if (user.IsAdmin)
        {
            await RequireAnotherAdminAsync(user.Id, ct);
        }

        await grants.RemoveForSubjectAsync(GrantSubject.User(user.Id), ct);
        await users.RemoveAsync(user.Id, ct);
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> roles)
    {
        var cleaned = roles
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unknown = cleaned.Where(r => !RoleNames.All.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(
                nameof(UpdateUserRolesRequest.Roles),
                $"Unknown role: {string.Join(", ", unknown)}. Known roles: {string.Join(", ", RoleNames.All)}.");
        }

        // Store the canonical spelling, so a role that arrived as "Admin" still matches the policy.
        return [.. cleaned.Select(r => RoleNames.All.First(known => string.Equals(known, r, StringComparison.OrdinalIgnoreCase)))];
    }

    private async Task<DirectoryUser> FindAsync(string userId, CancellationToken ct)
        => await users.FindByIdAsync(userId, ct) ?? throw new NotFoundException($"User '{userId}' not found.");

    private async Task RequireAnotherAdminAsync(string excludingId, CancellationToken ct)
    {
        var others = (await users.ListAsync(ct)).Any(u => u.IsAdmin && !string.Equals(u.Id, excludingId, StringComparison.Ordinal));
        if (!others)
        {
            throw new ConflictException("That's the only administrator left.");
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
