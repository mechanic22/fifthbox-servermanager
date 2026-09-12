using FifthBox.ServerManager.Storage.Abstractions;
using FifthBox.ServerManager.Storage.Models;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

/// <summary>EF-backed profile store. Thin adapter — the only nuance is upsert keeping CreatedAt.</summary>
public class EfUserProfileStore(AppDbContext db) : IUserProfileStore
{
    public Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task<UserProfile?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.ToLower();
        return db.UserProfiles.FirstOrDefaultAsync(p => p.Email.ToLower() == normalized, ct);
    }

    public async Task UpsertAsync(UserProfile profile, CancellationToken ct = default)
    {
        var existing = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == profile.UserId, ct);
        if (existing is null)
        {
            db.UserProfiles.Add(profile);
        }
        else
        {
            // Update in place; an update leaves the original CreatedAt untouched (INSERT ... ON CONFLICT).
            existing.Email = profile.Email;
            existing.FirstName = profile.FirstName;
            existing.LastName = profile.LastName;
            existing.UpdatedAt = profile.UpdatedAt;
        }

        await db.SaveChangesAsync(ct);
    }
}
