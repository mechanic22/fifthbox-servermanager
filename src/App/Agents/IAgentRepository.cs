namespace FifthBox.ServerManager.App.Agents;

/// Persistence port for agents. Implemented by Storage (EF). Pure persistence — no rules.
public interface IAgentRepository
{
    Task<IReadOnlyList<Agent>> ListAsync(CancellationToken ct = default);
    Task<Agent?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Agent agent, CancellationToken ct = default);
    Task UpdateAsync(Agent agent, CancellationToken ct = default);
    Task RemoveAsync(Agent agent, CancellationToken ct = default);
}

/// Stores the single active enrollment key's hash (revoked/rotated by replacing it).
public interface IEnrollmentKeyStore
{
    Task<string?> GetHashAsync(CancellationToken ct = default);
    Task SetHashAsync(string hash, CancellationToken ct = default);
}
