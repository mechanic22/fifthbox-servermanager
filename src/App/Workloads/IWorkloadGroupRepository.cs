namespace FifthBox.ServerManager.App.Workloads;

/// Persistence port for workload groups. Implemented by Storage (EF). Pure persistence — no rules.
public interface IWorkloadGroupRepository
{
    Task<IReadOnlyList<WorkloadGroup>> ListAsync(CancellationToken ct = default);
    Task<WorkloadGroup?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(WorkloadGroup group, CancellationToken ct = default);
    Task UpdateAsync(WorkloadGroup group, CancellationToken ct = default);
    Task RemoveAsync(WorkloadGroup group, CancellationToken ct = default);
}
