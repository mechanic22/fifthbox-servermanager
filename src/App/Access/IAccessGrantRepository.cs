using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

public readonly record struct GrantSubject(AccessSubject Type, string Id)
{
    public static GrantSubject User(string id) => new(AccessSubject.User, id);
    public static GrantSubject Team(string id) => new(AccessSubject.Team, id);
}

public interface IAccessGrantRepository
{
    Task<IReadOnlyList<AccessGrant>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AccessGrant>> ListForSubjectsAsync(IReadOnlyList<GrantSubject> subjects, CancellationToken ct = default);

    Task<AccessGrant?> FindAsync(GrantSubject subject, AccessScope scope, string targetId, CancellationToken ct = default);
    Task<AccessGrant?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(AccessGrant grant, CancellationToken ct = default);
    Task UpdateAsync(AccessGrant grant, CancellationToken ct = default);
    Task RemoveAsync(AccessGrant grant, CancellationToken ct = default);

    Task RemoveForTargetAsync(AccessScope scope, string targetId, CancellationToken ct = default);

    Task RemoveForSubjectAsync(GrantSubject subject, CancellationToken ct = default);
}
