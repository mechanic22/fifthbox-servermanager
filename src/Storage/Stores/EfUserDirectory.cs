using FifthBox.ServerManager.App.Access;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

/// EF implementation of <see cref="IUserDirectory"/>. Thin adapter — no business rules. Reads go
/// straight at the table Storage owns; writes go through the Identity package's own store and service
/// so its model stays the authority on what an account is.
public class EfUserDirectory(AppDbContext db, IUserStore users, IIdentityService identity) : IUserDirectory
{
    public async Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default)
    {
        // Roles is an IList<string> behind a value converter, so a role test can't run in SQL.
        var all = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        return [.. all.Select(Map)];
    }

    public async Task<DirectoryUser?> FindByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : Map(user);
    }

    public async Task SetRolesAsync(string id, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);
        if (user is null)
        {
            return;
        }

        user.Roles = [.. roles];
        await users.UpdateAsync(user, ct);
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        // The package clears the identity data it owns (refresh tokens, external logins, the account);
        // the profile is this app's sidecar, keyed by the same id, so it's ours to drop.
        await identity.DeleteUserAsync(id, ct);
        await db.UserProfiles.Where(p => p.UserId == id).ExecuteDeleteAsync(ct);
    }

    private static DirectoryUser Map(IdentityUser u) => new(u.Id, u.UserName, [.. u.Roles]);
}
