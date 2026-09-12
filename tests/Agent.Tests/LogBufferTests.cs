using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class LogBufferTests
{
    private static WorkloadLogLine Line(string text) => new() { Text = text };

    [TestMethod]
    public void Keeps_the_newest_lines_once_full()
    {
        var buffer = new LogBuffer(3);
        foreach (var n in new[] { "1", "2", "3", "4", "5" })
        {
            buffer.Add(Line(n));
        }

        CollectionAssert.AreEqual(new[] { "3", "4", "5" }, buffer.Tail(10).Select(l => l.Text).ToArray());
    }

    [TestMethod]
    public void Tail_returns_the_most_recent_lines_oldest_first()
    {
        var buffer = new LogBuffer(10);
        foreach (var n in new[] { "a", "b", "c" })
        {
            buffer.Add(Line(n));
        }

        CollectionAssert.AreEqual(new[] { "b", "c" }, buffer.Tail(2).Select(l => l.Text).ToArray());
    }

    [TestMethod]
    public void An_empty_buffer_tails_to_nothing()
    {
        Assert.IsEmpty(new LogBuffer(5).Tail(10));
    }

    [TestMethod]
    public void A_non_positive_tail_returns_nothing()
    {
        var buffer = new LogBuffer(5);
        buffer.Add(Line("x"));

        Assert.IsEmpty(buffer.Tail(0));
    }

    [TestMethod]
    public void Capacity_is_never_below_one()
    {
        var buffer = new LogBuffer(0);
        buffer.Add(Line("only"));

        Assert.AreEqual(1, buffer.Capacity);
        Assert.AreEqual("only", buffer.Tail(5).Single().Text);
    }
}
