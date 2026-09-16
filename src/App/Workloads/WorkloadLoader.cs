using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Workloads;

public sealed class WorkloadLoader(IWorkloadRepository repository, IWorkloadAccess access)
{
    /// the only load path so the access check can't be skipped, also hands back the level
    public async Task<(Workload Workload, AccessLevel Level)> LoadAsync(
        Caller caller, string id, AccessLevel needed, CancellationToken ct)
    {
        var workload = await repository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"Workload '{id}' not found.");

        var level = (await access.MapAsync(caller, ct)).ForWorkload(workload.Id, workload.GroupId);

        // below View, not allowed and not found must look the same or ids are probeable
        if (level < AccessLevel.View)
        {
            throw new NotFoundException($"Workload '{id}' not found.");
        }

        if (level < needed)
        {
            throw new ForbiddenException($"You don't have permission to do that to '{workload.Name}'.");
        }

        return (workload, level);
    }

    public static void RequireAdmin(Caller caller)
    {
        if (!caller.IsAdmin)
        {
            throw new ForbiddenException("Only an administrator can do that.");
        }
    }
}
