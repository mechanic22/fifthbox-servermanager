using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class RestartDecisionTests
{
    [TestMethod]
    public void Operator_stop_never_restarts_whatever_the_policy()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Always, exitCode: 0, stopRequested: true, attempt: 0);

        Assert.IsFalse(verdict.Restart);
    }

    [TestMethod]
    public void Never_policy_does_not_restart_on_a_crash()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Never, exitCode: 1, stopRequested: false, attempt: 0);

        Assert.IsFalse(verdict.Restart);
    }

    [TestMethod]
    public void OnFailure_restarts_a_nonzero_exit()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.OnFailure, exitCode: 1, stopRequested: false, attempt: 0);

        Assert.IsTrue(verdict.Restart);
    }

    [TestMethod]
    public void OnFailure_leaves_a_clean_exit_alone()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.OnFailure, exitCode: 0, stopRequested: false, attempt: 0);

        Assert.IsFalse(verdict.Restart);
    }

    [TestMethod]
    public void Always_restarts_even_a_clean_exit()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Always, exitCode: 0, stopRequested: false, attempt: 0);

        Assert.IsTrue(verdict.Restart);
    }

    [TestMethod]
    public void Backoff_grows_with_each_attempt()
    {
        var first = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: 0).Delay;
        var second = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: 1).Delay;
        var third = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: 2).Delay;

        Assert.AreEqual(TimeSpan.FromSeconds(2), first);
        Assert.AreEqual(TimeSpan.FromSeconds(4), second);
        Assert.AreEqual(TimeSpan.FromSeconds(8), third);
    }

    [TestMethod]
    public void Backoff_is_capped_at_a_minute()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: 9);

        Assert.AreEqual(TimeSpan.FromSeconds(60), verdict.Delay);
    }

    [TestMethod]
    public void Gives_up_once_the_attempt_cap_is_reached()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: RestartDecision.MaxAttempts);

        Assert.IsFalse(verdict.Restart, "a crash loop must not restart forever");
        StringAssert.Contains(verdict.Reason!, "restart limit");
    }

    [TestMethod]
    public void Attempt_just_below_the_cap_still_restarts()
    {
        var verdict = RestartDecision.Decide(RestartPolicy.Always, 1, false, attempt: RestartDecision.MaxAttempts - 1);

        Assert.IsTrue(verdict.Restart);
    }

    [TestMethod]
    public void A_run_that_lasted_past_the_window_counts_as_a_recovery()
    {
        Assert.IsTrue(RestartDecision.Recovered(RestartDecision.SuccessWindow));
        Assert.IsTrue(RestartDecision.Recovered(TimeSpan.FromHours(6)));
    }

    [TestMethod]
    public void A_run_that_died_inside_the_window_is_still_the_same_crash_run()
    {
        Assert.IsFalse(RestartDecision.Recovered(TimeSpan.Zero));
        Assert.IsFalse(RestartDecision.Recovered(RestartDecision.SuccessWindow - TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public void A_recovered_workload_gets_the_whole_budget_back()
    {
        // The point of the reset: at the cap, the same exit that would have been given up on restarts
        // again — and does it on the base delay, not the capped one.
        var givenUp = RestartDecision.Decide(RestartPolicy.Always, 1, false, RestartDecision.MaxAttempts);
        Assert.IsFalse(givenUp.Restart);

        var afterReset = RestartDecision.Decide(RestartPolicy.Always, 1, false, 0);
        Assert.IsTrue(afterReset.Restart);
        Assert.AreEqual(TimeSpan.FromSeconds(2), afterReset.Delay);
    }
}
