using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public interface IWorkloadGroupService
{
    Task<IReadOnlyList<WorkloadGroupResponse>> ListAsync(Caller caller, CancellationToken ct = default);
    Task<WorkloadGroupResponse> CreateAsync(Caller caller, CreateWorkloadGroupRequest request, CancellationToken ct = default);
    Task<WorkloadGroupResponse> RenameAsync(Caller caller, string id, UpdateWorkloadGroupRequest request, CancellationToken ct = default);

    /// Deletes the group, reparenting its subgroups up to its own parent and ungrouping its direct
    /// workloads. Nothing else is removed.
    Task DeleteAsync(Caller caller, string id, CancellationToken ct = default);
}

public sealed class WorkloadGroupService(
    IWorkloadGroupRepository groups,
    IWorkloadRepository workloads,
    IWorkloadAccess access,
    IAccessGrantRepository accessGrants,
    TimeProvider clock) : IWorkloadGroupService
{
    /// Groups the caller can see, plus the ancestors that connect them to the root. Without those
    /// ancestors a granted subgroup would orphan-parent to the top of the tree and lose its context.
    public async Task<IReadOnlyList<WorkloadGroupResponse>> ListAsync(Caller caller, CancellationToken ct = default)
    {
        var map = await access.MapAsync(caller, ct);
        var all = await groups.ListAsync(ct);
        var byId = all.ToDictionary(g => g.Id, StringComparer.Ordinal);

        var visibleWorkloads = (await workloads.ListAsync(ct))
            .Where(w => map.ForWorkload(w.Id, w.GroupId) >= AccessLevel.View)
            .ToList();

        var reachable = all.Where(g => map.ForGroup(g.Id) >= AccessLevel.View).Select(g => g.Id)
            .Concat(visibleWorkloads.Where(w => w.GroupId is not null).Select(w => w.GroupId!));

        var visible = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in reachable)
        {
            foreach (var ancestor in GroupChain.SelfAndAncestors(id, byId))
            {
                visible.Add(ancestor);
            }
        }

        var counts = visibleWorkloads
            .Where(w => w.GroupId is not null)
            .GroupBy(w => w.GroupId!)
            .ToDictionary(g => g.Key, g => g.Count());

        return all
            .Where(g => visible.Contains(g.Id))
            .Select(g => ToResponse(g, counts, map.ForGroup(g.Id)))
            .ToList();
    }

    public async Task<WorkloadGroupResponse> CreateAsync(Caller caller, CreateWorkloadGroupRequest request, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var name = ValidateName(request.Name);
        var all = await groups.ListAsync(ct);
        var parentId = ResolveParent(request.ParentId, all);
        RequireUniqueSibling(all, name, parentId, excludingId: null);

        var now = clock.GetUtcNow();
        var group = new WorkloadGroup { Name = name, ParentId = parentId, CreatedAt = now, UpdatedAt = now };
        await groups.AddAsync(group, ct);
        return new WorkloadGroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            ParentId = group.ParentId,
            WorkloadCount = 0,
            Access = AccessLevel.Configure,
        };
    }

    public async Task<WorkloadGroupResponse> RenameAsync(Caller caller, string id, UpdateWorkloadGroupRequest request, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var all = await groups.ListAsync(ct);
        var group = all.FirstOrDefault(g => g.Id == id) ?? throw new NotFoundException($"Group '{id}' not found.");
        var name = ValidateName(request.Name);
        RequireUniqueSibling(all, name, group.ParentId, excludingId: id);

        group.Name = name;
        group.UpdatedAt = clock.GetUtcNow();
        await groups.UpdateAsync(group, ct);

        var counts = await GroupCountsAsync(ct);
        return ToResponse(group, counts, AccessLevel.Configure);
    }

    public async Task DeleteAsync(Caller caller, string id, CancellationToken ct = default)
    {
        RequireAdmin(caller);
        var all = await groups.ListAsync(ct);
        var group = all.FirstOrDefault(g => g.Id == id) ?? throw new NotFoundException($"Group '{id}' not found.");

        // Reparent direct children up to the deleted group's own parent, then ungroup its direct workloads.
        foreach (var child in all.Where(g => g.ParentId == id))
        {
            child.ParentId = group.ParentId;
            child.UpdatedAt = clock.GetUtcNow();
            await groups.UpdateAsync(child, ct);
        }

        await workloads.ClearGroupAsync(id, ct);
        await groups.RemoveAsync(group, ct);

        // Drop the grants rather than reparenting them: moving a grant up to the deleted group's parent
        // would silently widen it across every sibling subtree.
        await accessGrants.RemoveForTargetAsync(AccessScope.Group, id, ct);
    }

    private static void RequireAdmin(Caller caller)
    {
        if (!caller.IsAdmin)
        {
            throw new ForbiddenException("Only an administrator can do that.");
        }
    }

    private static string? ResolveParent(string? parentId, IReadOnlyList<WorkloadGroup> all)
    {
        if (string.IsNullOrEmpty(parentId))
        {
            return null;
        }

        if (all.All(g => g.Id != parentId))
        {
            throw new ValidationException(nameof(CreateWorkloadGroupRequest.ParentId), "Parent group not found.");
        }

        return parentId;
    }

    private static void RequireUniqueSibling(IReadOnlyList<WorkloadGroup> all, string name, string? parentId, string? excludingId)
    {
        var clash = all.Any(g => g.Id != excludingId
            && g.ParentId == parentId
            && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (clash)
        {
            throw new ConflictException($"A group named '{name}' already exists here.");
        }
    }

    private static WorkloadGroupResponse ToResponse(WorkloadGroup g, IReadOnlyDictionary<string, int> counts, AccessLevel access) => new()
    {
        Id = g.Id,
        Name = g.Name,
        ParentId = g.ParentId,
        WorkloadCount = counts.GetValueOrDefault(g.Id),
        Access = access,
    };

    private async Task<Dictionary<string, int>> GroupCountsAsync(CancellationToken ct)
        => (await workloads.ListAsync(ct))
            .Where(w => w.GroupId is not null)
            .GroupBy(w => w.GroupId!)
            .ToDictionary(g => g.Key, g => g.Count());

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException(nameof(CreateWorkloadGroupRequest.Name), "A group name is required.");
        }

        return trimmed;
    }
}
