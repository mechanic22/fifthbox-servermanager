namespace FifthBox.ServerManager.App.Workloads;

/// Whether a workload's daily restart is due right now. Pure, because the interesting part is entirely
/// about clocks and it is the part worth being sure of.
public static class ScheduledRestart
{
    /// How late a restart may run. Without a limit, a Host that was down overnight would come back and
    /// bounce every scheduled workload at once, in the middle of the day — the opposite of the point.
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    /// <param name="localNow">Local time, because an operator picks "05:00" meaning their morning.</param>
    public static bool IsDue(int? dailyAtMinutes, DateTimeOffset localNow, DateTimeOffset? lastRunAt)
    {
        if (dailyAtMinutes is not { } minutes)
        {
            return false;
        }

        // Everything stays offset-aware. Comparing a bare DateTime against DateTimeOffset.LocalDateTime
        // re-converts the timezone, and on any host that isn't UTC the dedupe below then fails — which
        // means restarting every tick for the whole grace window.
        var scheduled = new DateTimeOffset(localNow.Date.AddMinutes(minutes), localNow.Offset);
        var elapsed = localNow - scheduled;

        if (elapsed < TimeSpan.Zero || elapsed > Grace)
        {
            return false;
        }

        // Once per occurrence: a five-minute tick would otherwise restart it repeatedly for an hour.
        return lastRunAt is not { } last || last < scheduled;
    }
}
