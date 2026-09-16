using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Stores;

/// secrets live on the workload and every revision, rotate both or revert breaks
public sealed class WorkloadSecretStore(AppDbContext db) : IProtectedSecretStore
{
    public string Name => "workload environment";

    public async Task<string?> SampleAsync(CancellationToken ct = default)
        => (await db.Workloads.AsNoTracking().ToListAsync(ct))
            .SelectMany(w => w.Env)
            .FirstOrDefault(v => v.Secret)?.Value;

    public async Task RewriteAsync(Func<string, string> rewrite, CancellationToken ct = default)
    {
        foreach (var workload in await db.Workloads.ToListAsync(ct))
        {
            workload.Env = Rewrite(workload.Env, rewrite);
            foreach (var revision in workload.Revisions)
            {
                revision.Env = Rewrite(revision.Env, rewrite);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static List<EnvVar> Rewrite(List<EnvVar> env, Func<string, string> rewrite) =>
        env.Select(v => v.Secret ? v with { Value = rewrite(v.Value) } : v).ToList();
}
