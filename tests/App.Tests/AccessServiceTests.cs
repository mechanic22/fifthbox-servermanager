using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using Moq;

using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AccessServiceTests
{
    private static readonly IReadOnlyList<string> Admin = [RoleNames.Admin];
    private static readonly IReadOnlyList<string> NoRoles = [];
    private static readonly DirectoryUser Friend = new("u1", "friend", NoRoles);
    private static readonly DirectoryUser Boss = new("u2", "boss", Admin);

    private static (AccessService svc, Mock<IAccessGrantRepository> grants) Build(
        AccessGrant? existing = null, params DirectoryUser[] directory)
    {
        var users = new Mock<IUserDirectory>();
        var known = directory.Length == 0 ? [Friend, Boss] : directory.ToList();
        users.Setup(u => u.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(known);
        users.Setup(u => u.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => known.FirstOrDefault(u => u.Id == id));

        var workloads = new Mock<IWorkloadRepository>();
        var workload = new Workload { Id = "w1", Name = "site" };
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([workload]);
        workloads.Setup(w => w.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "w1" ? workload : null);

        var groups = new Mock<IWorkloadGroupRepository>();
        var group = new WorkloadGroup { Id = "g1", Name = "games" };
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([group]);
        groups.Setup(g => g.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "g1" ? group : null);

        var teams = TeamRepo.Of(new Team { Id = "t1", Name = "ops" });

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing is null ? [] : new List<AccessGrant> { existing });
        grants.Setup(g => g.FindAsync(It.IsAny<GrantSubject>(), It.IsAny<AccessScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        grants.Setup(g => g.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        grants.Setup(g => g.UpdateAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        grants.Setup(g => g.RemoveAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new AccessService(grants.Object, users.Object, teams, workloads.Object, groups.Object, TimeProvider.System), grants);
    }

    private static SetAccessGrantRequest Request(AccessLevel level, AccessScope scope = AccessScope.Workload, string target = "w1")
        => new() { SubjectId = "u1", Scope = scope, TargetId = target, Level = level };

    [TestMethod]
    public async Task SetAsync_adds_a_grant_when_the_user_has_none()
    {
        var (svc, grants) = Build();

        var result = await svc.SetAsync(Request(AccessLevel.Operate));

        Assert.IsNotNull(result);
        Assert.AreEqual(AccessLevel.Operate, result.Level);
        Assert.AreEqual("site", result.TargetName);
        Assert.AreEqual("friend", result.SubjectName);
        grants.Verify(g => g.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()), Times.Once);
        grants.Verify(g => g.UpdateAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetAsync_updates_rather_than_duplicating_an_existing_grant()
    {
        var existing = new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View };
        var (svc, grants) = Build(existing);

        var result = await svc.SetAsync(Request(AccessLevel.Configure));

        Assert.IsNotNull(result);
        Assert.AreEqual(AccessLevel.Configure, result.Level);
        Assert.AreEqual(existing.Id, result.Id);
        grants.Verify(g => g.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        grants.Verify(g => g.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetAsync_with_None_removes_the_grant()
    {
        var existing = new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.Operate };
        var (svc, grants) = Build(existing);

        var result = await svc.SetAsync(Request(AccessLevel.None));

        Assert.IsNull(result);
        grants.Verify(g => g.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SetAsync_with_None_is_a_no_op_when_there_was_no_grant()
    {
        var (svc, grants) = Build();

        Assert.IsNull(await svc.SetAsync(Request(AccessLevel.None)));
        grants.Verify(g => g.RemoveAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetAsync_rejects_an_unknown_user()
    {
        var (svc, _) = Build();
        var request = Request(AccessLevel.View);
        request.SubjectId = "nobody";

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.SetAsync(request));
    }

    [TestMethod]
    public async Task SetAsync_rejects_granting_to_an_admin()
    {
        var (svc, _) = Build();
        var request = Request(AccessLevel.View);
        request.SubjectId = "u2";

        var error = await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.SetAsync(request));
        StringAssert.Contains(error.Errors[nameof(SetAccessGrantRequest.SubjectId)].Single(), "administrator");
    }

    [TestMethod]
    public async Task SetAsync_rejects_an_unknown_workload()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.SetAsync(Request(AccessLevel.View, target: "gone")));
    }

    [TestMethod]
    public async Task SetAsync_rejects_an_unknown_group()
    {
        var (svc, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => svc.SetAsync(Request(AccessLevel.View, AccessScope.Group, "gone")));
    }

    [TestMethod]
    public async Task ListAsync_resolves_the_user_and_target_names()
    {
        var existing = new AccessGrant { SubjectId = "u1", Scope = AccessScope.Group, TargetId = "g1", Level = AccessLevel.View };
        var (svc, _) = Build(existing);

        var listed = await svc.ListAsync();

        Assert.HasCount(1, listed);
        Assert.AreEqual("friend", listed[0].SubjectName);
        Assert.AreEqual("games", listed[0].TargetName);
    }

    [TestMethod]
    public async Task ListAsync_leaves_the_name_blank_for_a_deleted_target()
    {
        var orphan = new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "gone", Level = AccessLevel.View };
        var (svc, _) = Build(orphan);

        var listed = await svc.ListAsync();

        Assert.AreEqual(string.Empty, listed[0].TargetName);
    }

    [TestMethod]
    public async Task RemoveAsync_rejects_an_unknown_grant()
    {
        var (svc, grants) = Build();
        grants.Setup(g => g.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AccessGrant?)null);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.RemoveAsync("nope"));
    }

    [TestMethod]
    public async Task A_team_can_hold_a_grant()
    {
        var (svc, _) = Build();

        var grant = await svc.SetAsync(new SetAccessGrantRequest
        {
            SubjectType = AccessSubject.Team,
            SubjectId = "t1",
            Scope = AccessScope.Workload,
            TargetId = "w1",
            Level = AccessLevel.Operate,
        });

        Assert.IsNotNull(grant);
        Assert.AreEqual(AccessSubject.Team, grant.SubjectType);
        Assert.AreEqual("ops", grant.SubjectName);
    }

    [TestMethod]
    public async Task An_unknown_team_is_rejected()
    {
        var (svc, _) = Build();

        var error = await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.SetAsync(new SetAccessGrantRequest
        {
            SubjectType = AccessSubject.Team,
            SubjectId = "ghost",
            Scope = AccessScope.Workload,
            TargetId = "w1",
            Level = AccessLevel.View,
        }));

        StringAssert.Contains(error.Errors[nameof(SetAccessGrantRequest.SubjectId)].Single(), "Team not found");
    }

    [TestMethod]
    public async Task A_team_grant_is_not_blocked_by_an_admin_member()
    {
        // The admin rejection guards a *user* grant, which would sit dormant and come back on demotion.
        // A roster is different: the team is the thing being granted to, and it outlives the promotion.
        var (svc, _) = Build();

        var grant = await svc.SetAsync(new SetAccessGrantRequest
        {
            SubjectType = AccessSubject.Team,
            SubjectId = "t1",
            Scope = AccessScope.Group,
            TargetId = "g1",
            Level = AccessLevel.View,
        });

        Assert.IsNotNull(grant);
    }

    [TestMethod]
    public async Task ListAsync_resolves_a_team_name()
    {
        var existing = new AccessGrant
        {
            SubjectType = AccessSubject.Team,
            SubjectId = "t1",
            Scope = AccessScope.Workload,
            TargetId = "w1",
            Level = AccessLevel.View,
        };
        var (svc, _) = Build(existing);

        var listed = await svc.ListAsync();

        Assert.AreEqual("ops", listed[0].SubjectName);
    }

    // ListForTargetAsync answers "who can reach this thing", which is not the same question as
    // "what grants name it" — a grant on a group above reaches here too, and must say so.
    private static (AccessService Svc, Mock<IAccessGrantRepository> Grants) BuildTree(params AccessGrant[] grants)
    {
        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Friend, Boss]);
        users.Setup(u => u.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "u1" ? Friend : null);

        var teams = TeamRepo.Of(new Team { Id = "t1", Name = "ops" });

        var workload = new Workload { Id = "w1", Name = "site", GroupId = "acme" };
        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "w1" ? workload : null);
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([workload]);

        List<WorkloadGroup> tree =
        [
            new() { Id = "clients", Name = "clients" },
            new() { Id = "acme", Name = "acme", ParentId = "clients" },
            new() { Id = "other", Name = "other" },
        ];
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tree);
        groups.Setup(g => g.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => tree.FirstOrDefault(g => g.Id == id));

        var grantRepo = new Mock<IAccessGrantRepository>();
        grantRepo.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(grants.ToList());

        return (new AccessService(grantRepo.Object, users.Object, teams, workloads.Object, groups.Object, TimeProvider.System), grantRepo);
    }

    private static AccessGrant On(AccessScope scope, string target, AccessLevel level = AccessLevel.View)
        => new() { SubjectId = "u1", Scope = scope, TargetId = target, Level = level };

    [TestMethod]
    public async Task A_workloads_access_list_marks_a_direct_grant_as_direct()
    {
        var (svc, _) = BuildTree(On(AccessScope.Workload, "w1"));

        var listed = await svc.ListForTargetAsync(AccessScope.Workload, "w1");

        Assert.HasCount(1, listed);
        Assert.IsFalse(listed[0].Inherited);
        Assert.AreEqual("site", listed[0].TargetName);
    }

    [TestMethod]
    public async Task A_workloads_access_list_includes_grants_from_every_group_above_it()
    {
        var (svc, _) = BuildTree(
            On(AccessScope.Group, "acme", AccessLevel.Operate),
            On(AccessScope.Group, "clients"));

        var listed = await svc.ListForTargetAsync(AccessScope.Workload, "w1");

        Assert.HasCount(2, listed);
        Assert.IsTrue(listed.All(g => g.Inherited));
        CollectionAssert.AreEquivalent(new[] { "acme", "clients" }, listed.Select(g => g.TargetName).ToArray());
    }

    [TestMethod]
    public async Task A_workloads_access_list_ignores_a_group_it_is_not_under()
    {
        var (svc, _) = BuildTree(On(AccessScope.Group, "other"));

        Assert.IsEmpty(await svc.ListForTargetAsync(AccessScope.Workload, "w1"));
    }

    [TestMethod]
    public async Task A_groups_own_grant_is_direct_and_its_parents_is_inherited()
    {
        var (svc, _) = BuildTree(
            On(AccessScope.Group, "acme"),
            On(AccessScope.Group, "clients"));

        var listed = await svc.ListForTargetAsync(AccessScope.Group, "acme");

        Assert.HasCount(2, listed);
        Assert.IsFalse(listed.Single(g => g.TargetName == "acme").Inherited);
        Assert.IsTrue(listed.Single(g => g.TargetName == "clients").Inherited);
    }

    [TestMethod]
    public async Task A_grant_on_a_child_group_does_not_show_on_the_parent()
    {
        // Inheritance only ever flows down; a grant below must not look like access to the parent.
        var (svc, _) = BuildTree(On(AccessScope.Group, "acme"));

        Assert.IsEmpty(await svc.ListForTargetAsync(AccessScope.Group, "clients"));
    }

    [TestMethod]
    public async Task Listing_access_for_something_that_is_not_there_is_a_404()
    {
        var (svc, _) = BuildTree();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.ListForTargetAsync(AccessScope.Workload, "ghost"));
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.ListForTargetAsync(AccessScope.Group, "ghost"));
    }

    [TestMethod]
    public async Task A_team_grant_from_an_ancestor_group_is_named_and_marked_inherited()
    {
        var (svc, _) = BuildTree(new AccessGrant
        {
            SubjectType = AccessSubject.Team,
            SubjectId = "t1",
            Scope = AccessScope.Group,
            TargetId = "clients",
            Level = AccessLevel.Operate,
        });

        var listed = await svc.ListForTargetAsync(AccessScope.Workload, "w1");

        Assert.AreEqual("ops", listed[0].SubjectName);
        Assert.IsTrue(listed[0].Inherited);
    }
}
