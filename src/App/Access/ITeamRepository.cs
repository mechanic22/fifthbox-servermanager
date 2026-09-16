namespace FifthBox.ServerManager.App.Access;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default);

    /// membership is a json column so this filters in memory, fine at our scale
    Task<IReadOnlyList<Team>> ListForUserAsync(string userId, CancellationToken ct = default);

    Task<Team?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Team team, CancellationToken ct = default);
    Task UpdateAsync(Team team, CancellationToken ct = default);
    Task RemoveAsync(Team team, CancellationToken ct = default);
}
