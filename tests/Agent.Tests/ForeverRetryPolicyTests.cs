using FifthBox.ServerManager.Agent;
using RetryContext = Microsoft.AspNetCore.SignalR.Client.RetryContext;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class ForeverRetryPolicyTests
{
    [TestMethod]
    public void It_never_gives_up()
    {
        // The whole point: the default policy returns null after four attempts, and a supervisor that
        // stopped trying looks exactly like one that is fine.
        var policy = new ForeverRetryPolicy();

        foreach (var count in new long[] { 0, 1, 5, 100, 100_000 })
        {
            Assert.IsNotNull(policy.NextRetryDelay(new RetryContext { PreviousRetryCount = count }),
                $"gave up after {count} attempts");
        }
    }

    [TestMethod]
    public void The_first_attempt_is_immediate_so_a_host_restart_is_barely_noticed()
    {
        Assert.AreEqual(TimeSpan.Zero, ForeverRetryPolicy.DelayFor(0));
    }

    [TestMethod]
    public void It_backs_off_and_then_holds_steady()
    {
        var delays = new long[] { 0, 1, 2, 3, 4, 5, 50 }.Select(ForeverRetryPolicy.DelayFor).ToList();

        CollectionAssert.AreEqual(
            new[] { 0, 2, 5, 10, 30 }.Select(s => TimeSpan.FromSeconds(s)).ToArray(),
            delays.Take(5).ToArray());

        // Capped, not growing: a Host down for an hour must not push retries out to hours.
        Assert.AreEqual(TimeSpan.FromSeconds(30), delays[^1]);
    }

    [TestMethod]
    public void The_delay_never_shrinks_as_attempts_climb()
    {
        var previous = TimeSpan.MinValue;

        for (long i = 0; i < 20; i++)
        {
            var delay = ForeverRetryPolicy.DelayFor(i);
            Assert.IsTrue(delay >= previous, $"attempt {i} backed off less than the one before");
            previous = delay;
        }
    }
}
