using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AccessMapTests
{
    private static WorkloadGroup Group(string id, string? parentId = null)
        => new() { Id = id, Name = id, ParentId = parentId };

    private static AccessGrant Grant(AccessScope scope, string targetId, AccessLevel level)
        => new() { SubjectId = "u1", Scope = scope, TargetId = targetId, Level = level };

    [TestMethod]
    public void Direct_workload_grant_applies()
    {
        var map = AccessMap.Build([Grant(AccessScope.Workload, "w1", AccessLevel.Operate)], []);

        Assert.AreEqual(AccessLevel.Operate, map.ForWorkload("w1", groupId: null));
    }

    [TestMethod]
    public void Group_grant_reaches_a_workload_two_levels_down()
    {
        var groups = new List<WorkloadGroup> { Group("top"), Group("mid", "top"), Group("leaf", "mid") };
        var map = AccessMap.Build([Grant(AccessScope.Group, "top", AccessLevel.View)], groups);

        Assert.AreEqual(AccessLevel.View, map.ForGroup("leaf"));
        Assert.AreEqual(AccessLevel.View, map.ForWorkload("w1", "leaf"));
    }

    [TestMethod]
    public void Effective_level_is_the_max_of_direct_and_inherited()
    {
        var groups = new List<WorkloadGroup> { Group("top"), Group("leaf", "top") };
        var map = AccessMap.Build(
            [Grant(AccessScope.Group, "top", AccessLevel.View), Grant(AccessScope.Workload, "w1", AccessLevel.Configure)],
            groups);

        Assert.AreEqual(AccessLevel.Configure, map.ForWorkload("w1", "leaf"));
    }

    [TestMethod]
    public void A_deeper_group_grant_raises_rather_than_replaces()
    {
        var groups = new List<WorkloadGroup> { Group("top"), Group("leaf", "top") };
        var map = AccessMap.Build(
            [Grant(AccessScope.Group, "top", AccessLevel.Configure), Grant(AccessScope.Group, "leaf", AccessLevel.View)],
            groups);

        // Nearest-ancestor-wins would give View here; max keeps the broader Configure.
        Assert.AreEqual(AccessLevel.Configure, map.ForGroup("leaf"));
    }

    [TestMethod]
    public void Grant_does_not_leak_to_a_sibling_subtree()
    {
        var groups = new List<WorkloadGroup> { Group("top"), Group("mine", "top"), Group("theirs", "top") };
        var map = AccessMap.Build([Grant(AccessScope.Group, "mine", AccessLevel.Configure)], groups);

        Assert.AreEqual(AccessLevel.None, map.ForGroup("theirs"));
        Assert.AreEqual(AccessLevel.None, map.ForWorkload("w1", "theirs"));
    }

    [TestMethod]
    public void Grant_on_a_child_does_not_reach_its_parent()
    {
        var groups = new List<WorkloadGroup> { Group("top"), Group("leaf", "top") };
        var map = AccessMap.Build([Grant(AccessScope.Group, "leaf", AccessLevel.Configure)], groups);

        Assert.AreEqual(AccessLevel.None, map.ForGroup("top"));
    }

    [TestMethod]
    public void Root_workload_without_a_direct_grant_has_no_access()
    {
        var map = AccessMap.Build([Grant(AccessScope.Group, "top", AccessLevel.Configure)], [Group("top")]);

        Assert.AreEqual(AccessLevel.None, map.ForWorkload("w1", groupId: null));
    }

    [TestMethod]
    public void Unknown_ids_have_no_access()
    {
        var map = AccessMap.Build([], []);

        Assert.AreEqual(AccessLevel.None, map.ForGroup("nope"));
        Assert.AreEqual(AccessLevel.None, map.ForWorkload("nope", "nope"));
    }

    [TestMethod]
    public void A_missing_parent_stops_the_walk()
    {
        var groups = new List<WorkloadGroup> { Group("leaf", "gone") };
        var map = AccessMap.Build([Grant(AccessScope.Group, "leaf", AccessLevel.View)], groups);

        Assert.AreEqual(AccessLevel.View, map.ForGroup("leaf"));
    }

    [TestMethod]
    public void A_parent_cycle_terminates_and_still_resolves()
    {
        var groups = new List<WorkloadGroup> { Group("a", "b"), Group("b", "a") };
        var map = AccessMap.Build([Grant(AccessScope.Group, "b", AccessLevel.Operate)], groups);

        Assert.AreEqual(AccessLevel.Operate, map.ForGroup("a"));
        Assert.AreEqual(AccessLevel.Operate, map.ForGroup("b"));
    }

    [TestMethod]
    public void Admin_map_grants_everything_without_a_single_grant()
    {
        Assert.AreEqual(AccessLevel.Configure, AccessMap.Admin.ForGroup("anything"));
        Assert.AreEqual(AccessLevel.Configure, AccessMap.Admin.ForWorkload("anything", groupId: null));
    }
}
