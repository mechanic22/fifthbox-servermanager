namespace FifthBox.ServerManager.App.Access;

/// Persistence port for teams. Implemented by Storage (EF). Pure persistence — no rules.
public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> ListAsync(CancellationToken ct = default);

    /// Every team the user belongs to. Membership is a JSON column, so this filters in memory — fine
    /// at this system's scale, and it's a join table the day teams outgrow that.
    Task<IReadOnlyList<Team>> ListForUserAsync(string userId, CancellationToken ct = default);

    Task<Team?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Team team, CancellationToken ct = default);
    Task UpdateAsync(Team team, CancellationToken ct = default);
    Task RemoveAsync(Team team, CancellationToken ct = default);
}
