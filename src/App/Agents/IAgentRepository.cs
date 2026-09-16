namespace FifthBox.ServerManager.App.Agents;

public interface IAgentRepository
{
    Task<IReadOnlyList<Agent>> ListAsync(CancellationToken ct = default);
    Task<Agent?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Agent agent, CancellationToken ct = default);
    Task UpdateAsync(Agent agent, CancellationToken ct = default);
    Task RemoveAsync(Agent agent, CancellationToken ct = default);
}

/// one active key hash, rotate by replacing it
public interface IEnrollmentKeyStore
{
    Task<string?> GetHashAsync(CancellationToken ct = default);
    Task SetHashAsync(string hash, CancellationToken ct = default);
}
