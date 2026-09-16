using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadGroupServiceTests
{
    private static readonly Caller Admin = new("admin", IsAdmin: true);
    private static readonly Caller Friend = new("u1", IsAdmin: false);

    private static (WorkloadGroupService svc, Mock<IWorkloadGroupRepository> groups, Mock<IWorkloadRepository> workloads) Build(
        params WorkloadGroup[] existing)
    {
        var (svc, groups, workloads, _) = BuildWithGrants(existing);
        return (svc, groups, workloads);
    }

    private static (WorkloadGroupService svc, Mock<IWorkloadGroupRepository> groups, Mock<IWorkloadRepository> workloads,
        Mock<IAccessGrantRepository> grants) BuildWithGrants(params WorkloadGroup[] existing)
    {
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing.ToList());
        groups.Setup(g => g.AddAsync(It.IsAny<WorkloadGroup>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        groups.Setup(g => g.UpdateAsync(It.IsAny<WorkloadGroup>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        groups.Setup(g => g.RemoveAsync(It.IsAny<WorkloadGroup>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var workloads = new Mock<IWorkloadRepository>();
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workload>());
        workloads.Setup(w => w.ClearGroupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        grants.Setup(g => g.RemoveForTargetAsync(It.IsAny<AccessScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new WorkloadGroupService(
            groups.Object, workloads.Object, new WorkloadAccess(grants.Object, groups.Object, TeamRepo.None()), grants.Object, TimeProvider.System);

        return (svc, groups, workloads, grants);
    }

    [TestMethod]
    public async Task Create_trims_and_persists_at_top_level()
    {
        var (svc, groups, _) = Build();
        WorkloadGroup? saved = null;
        groups.Setup(g => g.AddAsync(It.IsAny<WorkloadGroup>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadGroup, CancellationToken>((g, _) => saved = g).Returns(Task.CompletedTask);

        var result = await svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "  prod  " });

        Assert.AreEqual("prod", result.Name);
        Assert.AreEqual("prod", saved!.Name);
        Assert.IsNull(saved.ParentId);
    }

    [TestMethod]
    public async Task Create_blank_name_throws_validation()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "  " }));
    }

    [TestMethod]
    public async Task Create_under_parent_sets_parent_id()
    {
        var (svc, groups, _) = Build(new WorkloadGroup { Id = "p1", Name = "parent" });
        WorkloadGroup? saved = null;
        groups.Setup(g => g.AddAsync(It.IsAny<WorkloadGroup>(), It.IsAny<CancellationToken>()))
            .Callback<WorkloadGroup, CancellationToken>((g, _) => saved = g).Returns(Task.CompletedTask);

        var result = await svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "child", ParentId = "p1" });

        Assert.AreEqual("p1", saved!.ParentId);
        Assert.AreEqual("p1", result.ParentId);
    }

    [TestMethod]
    public async Task Create_with_unknown_parent_throws_validation()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(() =>
            svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "child", ParentId = "nope" }));
    }

    [TestMethod]
    public async Task Create_duplicate_name_among_siblings_throws_conflict()
    {
        var (svc, _, _) = Build(
            new WorkloadGroup { Id = "p1", Name = "p1" },
            new WorkloadGroup { Id = "g1", Name = "prod", ParentId = "p1" });

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "prod", ParentId = "p1" }));
    }

    [TestMethod]
    public async Task Create_same_name_under_different_parent_is_allowed()
    {
        var (svc, groups, _) = Build(
            new WorkloadGroup { Id = "p1", Name = "p1" },
            new WorkloadGroup { Id = "p2", Name = "p2" },
            new WorkloadGroup { Id = "g1", Name = "web", ParentId = "p1" });

        await svc.CreateAsync(Admin, new CreateWorkloadGroupRequest { Name = "web", ParentId = "p2" });

        groups.Verify(g => g.AddAsync(It.Is<WorkloadGroup>(x => x.Name == "web" && x.ParentId == "p2"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Delete_reparents_children_up_and_ungroups_workloads()
    {
        var parent = new WorkloadGroup { Id = "p1", Name = "p1" };
        var target = new WorkloadGroup { Id = "b", Name = "b", ParentId = "p1" };
        var child = new WorkloadGroup { Id = "c", Name = "c", ParentId = "b" };
        var (svc, groups, workloads) = Build(parent, target, child);

        await svc.DeleteAsync(Admin, "b");

        Assert.AreEqual("p1", child.ParentId);
        groups.Verify(g => g.UpdateAsync(It.Is<WorkloadGroup>(x => x.Id == "c" && x.ParentId == "p1"), It.IsAny<CancellationToken>()), Times.Once);
        workloads.Verify(w => w.ClearGroupAsync("b", It.IsAny<CancellationToken>()), Times.Once);
        groups.Verify(g => g.RemoveAsync(It.Is<WorkloadGroup>(x => x.Id == "b"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Delete_missing_group_throws_not_found()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.DeleteAsync(Admin, "x"));
    }

    [TestMethod]
    public async Task Shaping_the_tree_stays_with_the_administrator()
    {
        var (svc, _, _) = Build(new WorkloadGroup { Id = "g1", Name = "clients" });

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() =>
            svc.CreateAsync(Friend, new CreateWorkloadGroupRequest { Name = "mine" }));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() =>
            svc.RenameAsync(Friend, "g1", new UpdateWorkloadGroupRequest { Name = "theirs" }));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeleteAsync(Friend, "g1"));
    }

    [TestMethod]
    public async Task An_admin_sees_the_whole_tree()
    {
        var (svc, _, _) = Build(
            new WorkloadGroup { Id = "clients", Name = "clients" },
            new WorkloadGroup { Id = "acme", Name = "acme", ParentId = "clients" },
            new WorkloadGroup { Id = "internal", Name = "internal" });

        var list = await svc.ListAsync(Admin);

        Assert.HasCount(3, list);
        Assert.IsTrue(list.All(g => g.Access == AccessLevel.Configure));
    }

    [TestMethod]
    public async Task A_grant_on_a_subgroup_brings_its_ancestors_along_as_scaffolding()
    {
        var (svc, _, _, grants) = BuildWithGrants(
            new WorkloadGroup { Id = "clients", Name = "clients" },
            new WorkloadGroup { Id = "acme", Name = "acme", ParentId = "clients" },
            new WorkloadGroup { Id = "internal", Name = "internal" });
        grants.Setup(g => g.ListForSubjectsAsync(It.Is<IReadOnlyList<GrantSubject>>(s => s.Contains(GrantSubject.User("u1"))), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AccessGrant { SubjectId = "u1", Scope = AccessScope.Group, TargetId = "acme", Level = AccessLevel.Operate }]);

        var list = await svc.ListAsync(Friend);

        // acme is granted, clients only comes along so acme still nests, internal is hidden
        CollectionAssert.AreEquivalent(new[] { "acme", "clients" }, list.Select(g => g.Id).ToArray());
        Assert.AreEqual(AccessLevel.Operate, list.Single(g => g.Id == "acme").Access);
        Assert.AreEqual(AccessLevel.None, list.Single(g => g.Id == "clients").Access);
    }

    [TestMethod]
    public async Task The_workload_count_only_counts_what_the_caller_can_see()
    {
        var (svc, _, workloads, grants) = BuildWithGrants(new WorkloadGroup { Id = "acme", Name = "acme" });
        workloads.Setup(w => w.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Workload { Id = "w1", Name = "site", GroupId = "acme" },
            new Workload { Id = "w2", Name = "db", GroupId = "acme" },
        ]);
        grants.Setup(g => g.ListForSubjectsAsync(It.Is<IReadOnlyList<GrantSubject>>(s => s.Contains(GrantSubject.User("u1"))), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AccessGrant { SubjectId = "u1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View }]);

        var group = (await svc.ListAsync(Friend)).Single();

        // counting all of them tells a client how much they can't see
        Assert.AreEqual(1, group.WorkloadCount);
        Assert.AreEqual(AccessLevel.None, group.Access);
    }

    [TestMethod]
    public async Task Deleting_a_group_drops_the_grants_held_on_it()
    {
        var (svc, _, _, grants) = BuildWithGrants(new WorkloadGroup { Id = "g1", Name = "clients" });

        await svc.DeleteAsync(Admin, "g1");

        // dropped, not reparented, moving them up would widen them across sibling subtrees
        grants.Verify(g => g.RemoveForTargetAsync(AccessScope.Group, "g1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
