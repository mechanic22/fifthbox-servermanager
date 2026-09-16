using FifthBox.ServerManager.App.Access;
using FifthBox.Identity.Abstractions;
using FifthBox.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

/// reads hit the table directly, writes go through Identity so it stays the authority on accounts
public class EfUserDirectory(AppDbContext db, IUserStore users, IIdentityService identity) : IUserDirectory
{
    public async Task<IReadOnlyList<DirectoryUser>> ListAsync(CancellationToken ct = default)
    {
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
        // identity clears its own stuff, the profile sidecar is ours to drop
        await identity.DeleteUserAsync(id, ct);
        await db.UserProfiles.Where(p => p.UserId == id).ExecuteDeleteAsync(ct);
    }

    private static DirectoryUser Map(IdentityUser u) => new(u.Id, u.UserName, [.. u.Roles]);
}
