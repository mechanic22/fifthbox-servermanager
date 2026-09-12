using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Workloads;

/// Loads a workload and checks the caller may do what they are about to do. Shared, because both halves
/// of the workload surface start every operation this way and they must agree on the answer.
public sealed class WorkloadLoader(IWorkloadRepository repository, IWorkloadAccess access)
{
    /// The only way to load a workload here, so enforcement can't be forgotten on a method added later.
    /// Returns the resolved level too — Deploy and the secret gate both need it.
    public async Task<(Workload Workload, AccessLevel Level)> LoadAsync(
        Caller caller, string id, AccessLevel needed, CancellationToken ct)
    {
        var workload = await repository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"Workload '{id}' not found.");

        var level = (await access.MapAsync(caller, ct)).ForWorkload(workload.Id, workload.GroupId);

        // Below View, "you can't" and "it isn't there" have to look identical, or ids become probeable.
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
