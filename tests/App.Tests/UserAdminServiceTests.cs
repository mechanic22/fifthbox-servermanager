using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using Moq;

using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class UserAdminServiceTests
{
    private static readonly IReadOnlyList<string> Admin = [RoleNames.Admin];
    private static readonly IReadOnlyList<string> NoRoles = [];
    private static readonly Caller Boss = new("admin1", IsAdmin: true);
    private static readonly Caller Nobody = new("u1", IsAdmin: false);

    private static (UserAdminService svc, Mock<IUserDirectory> users, Mock<IAccessGrantRepository> grants) Build(
        params DirectoryUser[] directory)
    {
        var known = directory.Length == 0
            ? [new DirectoryUser("admin1", "boss", Admin), new DirectoryUser("u1", "friend", NoRoles)]
            : directory.ToList();

        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(known);
        users.Setup(u => u.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => known.FirstOrDefault(u => u.Id == id));
        users.Setup(u => u.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        users.Setup(u => u.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        grants.Setup(g => g.RemoveForSubjectAsync(It.IsAny<GrantSubject>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new UserAdminService(users.Object, grants.Object), users, grants);
    }

    [TestMethod]
    public async Task ListAsync_counts_grants_per_user_and_never_for_an_admin()
    {
        var (svc, _, grants) = Build();
        grants.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View },
            new AccessGrant { SubjectId = "u1", Scope = AccessScope.Group, TargetId = "g1", Level = AccessLevel.Operate },
            new AccessGrant { SubjectId = "admin1", Scope = AccessScope.Workload, TargetId = "w2", Level = AccessLevel.View },
        ]);

        var listed = await svc.ListAsync(Boss);

        Assert.AreEqual(2, listed.Single(u => u.Id == "u1").GrantCount);
        Assert.AreEqual(0, listed.Single(u => u.Id == "admin1").GrantCount);
    }

    [TestMethod]
    public async Task A_non_admin_caller_is_refused()
    {
        var (svc, _, _) = Build();

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.ListAsync(Nobody));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.SetRolesAsync(Nobody, "u1", Admin));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeleteAsync(Nobody, "u1"));
    }

    [TestMethod]
    public async Task An_unknown_user_is_not_found()
    {
        var (svc, _, _) = Build();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.SetRolesAsync(Boss, "ghost", Admin));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.DeleteAsync(Boss, "ghost"));
    }

    [TestMethod]
    public async Task You_cannot_change_your_own_role_or_delete_yourself()
    {
        var (svc, _, _) = Build();

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.SetRolesAsync(Boss, "admin1", NoRoles));
        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.DeleteAsync(Boss, "admin1"));
    }

    [TestMethod]
    public async Task The_last_administrator_survives_a_caller_whose_admin_claim_is_stale()
    {
        // The guard only bites when the caller claims admin but no longer is one — demoted while holding
        // a live cookie, whose claims outlive the row. Any genuine admin either is the last admin (and
        // the self-check fires first) or leaves another one behind.
        var stale = new Caller("ghost", IsAdmin: true);
        var (svc, _, _) = Build(
            new DirectoryUser("admin1", "boss", Admin),
            new DirectoryUser("u1", "friend", NoRoles));

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.SetRolesAsync(stale, "admin1", NoRoles));
        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.DeleteAsync(stale, "admin1"));
    }

    [TestMethod]
    public async Task An_administrator_can_be_demoted_while_another_one_remains()
    {
        var (svc, users, _) = Build(
            new DirectoryUser("admin1", "boss", Admin),
            new DirectoryUser("admin2", "deputy", Admin));

        await svc.SetRolesAsync(new Caller("admin2", true), "admin1", NoRoles);

        users.Verify(u => u.SetRolesAsync("admin1", It.Is<IReadOnlyList<string>>(r => r.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Promoting_someone_to_admin_clears_the_grants_the_role_makes_redundant()
    {
        var (svc, users, grants) = Build();

        await svc.SetRolesAsync(Boss, "u1", Admin);

        users.Verify(u => u.SetRolesAsync("u1", It.Is<IReadOnlyList<string>>(r => r.Contains(RoleNames.Admin)), It.IsAny<CancellationToken>()), Times.Once);
        grants.Verify(g => g.RemoveForSubjectAsync(GrantSubject.User("u1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Setting_the_roles_someone_already_has_writes_nothing()
    {
        var (svc, users, _) = Build();

        await svc.SetRolesAsync(Boss, "u1", NoRoles);

        users.Verify(u => u.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Deleting_a_user_takes_their_grants_with_them()
    {
        var (svc, users, grants) = Build();

        await svc.DeleteAsync(Boss, "u1");

        grants.Verify(g => g.RemoveForSubjectAsync(GrantSubject.User("u1"), It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(u => u.RemoveAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
