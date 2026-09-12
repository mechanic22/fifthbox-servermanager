using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class ScheduledRestartTests
{
    private const int FiveAm = 5 * 60;

    private static DateTimeOffset Local(int hour, int minute = 0, int day = 10) =>
        new(new DateTime(2026, 9, day, hour, minute, 0), TimeSpan.Zero);

    [TestMethod]
    public void No_schedule_is_never_due()
    {
        Assert.IsFalse(ScheduledRestart.IsDue(null, Local(5), null));
    }

    [TestMethod]
    public void It_is_due_at_the_scheduled_minute()
    {
        Assert.IsTrue(ScheduledRestart.IsDue(FiveAm, Local(5), null));
    }

    [TestMethod]
    public void It_is_not_due_before_the_time()
    {
        Assert.IsFalse(ScheduledRestart.IsDue(FiveAm, Local(4, 59), null));
    }

    [TestMethod]
    public void A_missed_window_is_skipped_rather_than_fired_hours_late()
    {
        // A Host that was down overnight must not come back and bounce everything at lunchtime.
        Assert.IsFalse(ScheduledRestart.IsDue(FiveAm, Local(12), null));
    }

    [TestMethod]
    public void It_fires_once_per_day_not_once_per_tick()
    {
        var ranToday = Local(5, 1);

        Assert.IsFalse(ScheduledRestart.IsDue(FiveAm, Local(5, 6), ranToday));
        Assert.IsFalse(ScheduledRestart.IsDue(FiveAm, Local(5, 55), ranToday));
    }

    [TestMethod]
    public void Yesterdays_run_does_not_satisfy_today()
    {
        var ranYesterday = Local(5, 1, day: 9);

        Assert.IsTrue(ScheduledRestart.IsDue(FiveAm, Local(5, 0), ranYesterday));
    }

    [TestMethod]
    public void Midnight_is_a_real_schedule_not_an_absent_one()
    {
        // 0 minutes past midnight is falsy-looking; it must not be confused with "no schedule".
        Assert.IsTrue(ScheduledRestart.IsDue(0, Local(0, 10), null));
    }

    [TestMethod]
    public void The_grace_window_is_the_boundary()
    {
        Assert.IsTrue(ScheduledRestart.IsDue(FiveAm, Local(5, 59), null));
        Assert.IsFalse(ScheduledRestart.IsDue(FiveAm, Local(6, 1), null));
    }
}
