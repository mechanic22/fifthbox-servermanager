using FifthBox.ServerManager.Agent;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class StopLadderTests
{
    [TestMethod]
    public void A_stop_command_is_tried_before_anything_else()
    {
        CollectionAssert.AreEqual(
            new[] { StopStep.ConsoleCommand, StopStep.Signal, StopStep.Kill },
            StopLadder.For("stop", hasConsole: true).ToArray());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Without_a_stop_command_there_is_nothing_to_write(string? stopCommand)
    {
        CollectionAssert.AreEqual(
            new[] { StopStep.Signal, StopStep.Kill },
            StopLadder.For(stopCommand, hasConsole: true).ToArray());
    }

    [TestMethod]
    public void An_adopted_process_has_no_console_to_write_to()
    {
        CollectionAssert.AreEqual(
            new[] { StopStep.Signal, StopStep.Kill },
            StopLadder.For("stop", hasConsole: false).ToArray());
    }

    [TestMethod]
    public void Kill_is_always_the_last_resort()
    {
        foreach (var steps in new[]
                 {
                     StopLadder.For("stop", true),
                     StopLadder.For(null, true),
                     StopLadder.For("stop", false),
                 })
        {
            Assert.AreEqual(StopStep.Kill, steps[^1]);
            Assert.AreEqual(1, steps.Count(s => s == StopStep.Kill), "one kill, at the end");
        }
    }
}
