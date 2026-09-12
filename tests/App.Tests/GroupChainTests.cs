using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class GroupChainTests
{
    private static Dictionary<string, WorkloadGroup> Tree(params (string Id, string? ParentId)[] groups)
        => groups.ToDictionary(g => g.Id, g => new WorkloadGroup { Id = g.Id, Name = g.Id, ParentId = g.ParentId });

    [TestMethod]
    public void Yields_self_first_then_ancestors_outward()
    {
        var byId = Tree(("top", null), ("mid", "top"), ("leaf", "mid"));

        CollectionAssert.AreEqual(new[] { "leaf", "mid", "top" }, GroupChain.SelfAndAncestors("leaf", byId).ToArray());
    }

    [TestMethod]
    public void No_group_yields_nothing()
    {
        Assert.AreEqual(0, GroupChain.SelfAndAncestors(null, Tree()).Count());
    }

    [TestMethod]
    public void Stops_at_a_parent_that_no_longer_exists()
    {
        var byId = Tree(("leaf", "gone"));

        CollectionAssert.AreEqual(new[] { "leaf", "gone" }, GroupChain.SelfAndAncestors("leaf", byId).ToArray());
    }

    [TestMethod]
    public void A_cycle_yields_each_group_once()
    {
        var byId = Tree(("a", "b"), ("b", "a"));

        CollectionAssert.AreEqual(new[] { "a", "b" }, GroupChain.SelfAndAncestors("a", byId).ToArray());
    }
}
