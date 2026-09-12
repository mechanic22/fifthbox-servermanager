namespace FifthBox.ServerManager.App.Registries;

/// Persistence for registry credentials. Implemented by Storage (EF). Pure persistence.
public interface IRegistryRepository
{
    Task<IReadOnlyList<Registry>> ListAsync(CancellationToken ct = default);
    Task<Registry?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Registry registry, CancellationToken ct = default);
    Task UpdateAsync(Registry registry, CancellationToken ct = default);
    Task RemoveAsync(Registry registry, CancellationToken ct = default);
}
