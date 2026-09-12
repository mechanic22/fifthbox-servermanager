using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Nodes;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class NodeSnapshotTests
{
    private static NodeResponse Node(string id, NodeStatus status = NodeStatus.Ready, NodeAvailability availability = NodeAvailability.Active) =>
        new() { Id = id, Hostname = id, Status = status, Availability = availability };

    [TestMethod]
    public void Identical_snapshots_do_not_differ()
    {
        var a = new[] { Node("1"), Node("2") };
        var b = new[] { Node("1"), Node("2") };
        Assert.IsFalse(NodeSnapshot.Differs(a, b));
    }

    [TestMethod]
    public void Order_is_ignored()
    {
        var a = new[] { Node("1"), Node("2") };
        var b = new[] { Node("2"), Node("1") };
        Assert.IsFalse(NodeSnapshot.Differs(a, b));
    }

    [TestMethod]
    public void Added_or_removed_node_differs()
    {
        Assert.IsTrue(NodeSnapshot.Differs(new[] { Node("1") }, new[] { Node("1"), Node("2") }));
        Assert.IsTrue(NodeSnapshot.Differs(new[] { Node("1"), Node("2") }, new[] { Node("1") }));
    }

    [TestMethod]
    public void Status_change_differs()
    {
        var a = new[] { Node("1", NodeStatus.Ready) };
        var b = new[] { Node("1", NodeStatus.Down) };
        Assert.IsTrue(NodeSnapshot.Differs(a, b));
    }

    [TestMethod]
    public void Availability_change_differs()
    {
        var a = new[] { Node("1", availability: NodeAvailability.Active) };
        var b = new[] { Node("1", availability: NodeAvailability.Drain) };
        Assert.IsTrue(NodeSnapshot.Differs(a, b));
    }

}
