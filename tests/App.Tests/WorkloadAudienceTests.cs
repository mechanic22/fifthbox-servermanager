using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Tests;

/// Who a server→client push is addressed to. Getting this wrong either leaks the inventory to everyone
/// or silently stops updates reaching someone who can see the workload.
[TestClass]
public class WorkloadAudienceTests
{
    private static readonly IReadOnlyList<string> Admin = [RoleNames.Admin];
    private static readonly IReadOnlyList<string> NoRoles = [];
    private static readonly Caller Friend = new("u1", IsAdmin: false);

    private static WorkloadAudience Build(
        Workload workload,
        IReadOnlyList<DirectoryUser> users,
        IReadOnlyList<AccessGrant>? grants = null,
        IReadOnlyList<WorkloadGroup>? groups = null,
        params Team[] teams)
    {
        var workloadRepo = new Mock<IWorkloadRepository>();
        workloadRepo.Setup(r => r.FindByIdAsync(workload.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var grantRepo = new Mock<IAccessGrantRepository>();
        grantRepo.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(grants?.ToList() ?? []);
        grantRepo.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<GrantSubject> subjects, CancellationToken _) =>
                grants?.Where(g => subjects.Contains(new GrantSubject(g.SubjectType, g.SubjectId))).ToList() ?? []);

        var groupRepo = new Mock<IWorkloadGroupRepository>();
        groupRepo.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups?.ToList() ?? []);

        var directory = new Mock<IUserDirectory>();
        directory.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users.ToList());

        var teamRepo = TeamRepo.Of(teams);
        return new WorkloadAudience(
            new WorkloadAccess(grantRepo.Object, groupRepo.Object, teamRepo),
            grantRepo.Object,
            groupRepo.Object,
            workloadRepo.Object,
            teamRepo,
            directory.Object);
    }

    private static Workload Workload(string? groupId = null)
        => new() { Id = "w1", Name = "web", GroupId = groupId, Kind = WorkloadKind.Container, Image = "nginx" };

    [TestMethod]
    public async Task Admins_are_in_the_audience_without_holding_a_grant()
    {
        var audience = Build(Workload(), [new DirectoryUser("admin", "admin@x", Admin)]);

        CollectionAssert.AreEqual(new[] { "admin" }, (await audience.ViewerIdsAsync("w1")).ToArray());
    }

    [TestMethod]
    public async Task A_user_with_no_grant_hears_nothing()
    {
        var audience = Build(Workload(), [new DirectoryUser("u1", "friend@x", NoRoles)]);

        Assert.IsEmpty(await audience.ViewerIdsAsync("w1"));
    }

    [TestMethod]
    public async Task A_grant_on_an_ancestor_group_puts_you_in_the_audience()
    {
        // The case that matters: u1 holds nothing on w1 itself, only on a group two levels above it.
        var groups = new List<WorkloadGroup>
        {
            new() { Id = "clients", Name = "clients" },
            new() { Id = "acme", Name = "acme", ParentId = "clients" },
        };
        var audience = Build(
            Workload(groupId: "acme"),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [new AccessGrant { SubjectId = "u1", Scope = AccessScope.Group, TargetId = "clients", Level = AccessLevel.View }],
            groups);

        CollectionAssert.AreEqual(new[] { "u1" }, (await audience.ViewerIdsAsync("w1")).ToArray());
    }

    [TestMethod]
    public async Task Holding_both_a_group_and_a_direct_grant_lists_you_once()
    {
        var groups = new List<WorkloadGroup> { new() { Id = "acme", Name = "acme" } };
        var audience = Build(
            Workload(groupId: "acme"),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [
                new AccessGrant { SubjectId = "u1", Scope = AccessScope.Group, TargetId = "acme", Level = AccessLevel.View },
                new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.Operate },
            ],
            groups);

        Assert.HasCount(1, await audience.ViewerIdsAsync("w1"));
    }

    [TestMethod]
    public async Task Following_logs_for_a_workload_you_cannot_see_is_refused()
    {
        var audience = Build(Workload(), [new DirectoryUser("u1", "friend@x", NoRoles)]);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => audience.RequireViewAsync(Friend, "w1"));
    }

    [TestMethod]
    public async Task Following_logs_is_allowed_at_view()
    {
        var audience = Build(
            Workload(),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }]);

        await audience.RequireViewAsync(Friend, "w1");
    }

    [TestMethod]
    public async Task A_team_grant_reaches_every_member()
    {
        var audience = Build(
            Workload(),
            [new DirectoryUser("u1", "friend@x", NoRoles), new DirectoryUser("u2", "client@x", NoRoles)],
            [new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }],
            null,
            new Team { Id = "t1", Name = "ops", MemberIds = ["u1", "u2"] });

        CollectionAssert.AreEquivalent(new[] { "u1", "u2" }, (await audience.ViewerIdsAsync("w1")).ToArray());
    }

    [TestMethod]
    public async Task A_team_grant_reaches_nobody_outside_the_team()
    {
        var audience = Build(
            Workload(),
            [new DirectoryUser("u1", "friend@x", NoRoles), new DirectoryUser("u2", "client@x", NoRoles)],
            [new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }],
            null,
            new Team { Id = "t1", Name = "ops", MemberIds = ["u1"] });

        CollectionAssert.AreEqual(new[] { "u1" }, (await audience.ViewerIdsAsync("w1")).ToArray());
    }

    [TestMethod]
    public async Task A_team_grant_on_an_ancestor_group_reaches_a_nested_workload()
    {
        var groups = new List<WorkloadGroup>
        {
            new() { Id = "clients", Name = "clients" },
            new() { Id = "acme", Name = "acme", ParentId = "clients" },
        };
        var audience = Build(
            Workload(groupId: "acme"),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Group, TargetId = "clients", Level = AccessLevel.View }],
            groups,
            new Team { Id = "t1", Name = "ops", MemberIds = ["u1"] });

        CollectionAssert.AreEqual(new[] { "u1" }, (await audience.ViewerIdsAsync("w1")).ToArray());
    }

    [TestMethod]
    public async Task Holding_both_a_personal_and_a_team_grant_lists_you_once()
    {
        var audience = Build(
            Workload(),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [
                new AccessGrant { SubjectType = AccessSubject.User, SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View },
                new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.Operate },
            ],
            null,
            new Team { Id = "t1", Name = "ops", MemberIds = ["u1"] });

        Assert.HasCount(1, await audience.ViewerIdsAsync("w1"));
    }

    [TestMethod]
    public async Task A_team_id_that_collides_with_a_user_id_does_not_cross_over()
    {
        // Nothing generates colliding ids, but the subject discriminator is the only thing keeping
        // these apart now that both live in one column.
        var audience = Build(
            Workload(),
            [new DirectoryUser("t1", "impostor@x", NoRoles)],
            [new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }],
            null,
            new Team { Id = "t1", Name = "ops", MemberIds = [] });

        Assert.IsEmpty(await audience.ViewerIdsAsync("w1"));
    }

    [TestMethod]
    public async Task Following_logs_is_allowed_through_a_team_grant()
    {
        var audience = Build(
            Workload(),
            [new DirectoryUser("u1", "friend@x", NoRoles)],
            [new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }],
            null,
            new Team { Id = "t1", Name = "ops", MemberIds = ["u1"] });

        await audience.RequireViewAsync(Friend, "w1");
    }
}
