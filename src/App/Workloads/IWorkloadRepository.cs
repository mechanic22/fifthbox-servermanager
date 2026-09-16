namespace FifthBox.ServerManager.App.Workloads;

public interface IWorkloadRepository
{
    Task<IReadOnlyList<Workload>> ListAsync(CancellationToken ct = default);
    Task<Workload?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Workload?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, string? excludingId = null, CancellationToken ct = default);

    Task ClearGroupAsync(string groupId, CancellationToken ct = default);

    Task AddAsync(Workload workload, CancellationToken ct = default);
    Task UpdateAsync(Workload workload, CancellationToken ct = default);
    Task RemoveAsync(Workload workload, CancellationToken ct = default);
}
