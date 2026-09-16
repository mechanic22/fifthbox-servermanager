namespace FifthBox.ServerManager.App.Workloads;

public static class ScheduledRestart
{
    /// without a cap a Host that was down overnight would bounce everything mid-day
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    /// localNow is local time, "05:00" means the operator's morning
    public static bool IsDue(int? dailyAtMinutes, DateTimeOffset localNow, DateTimeOffset? lastRunAt)
    {
        if (dailyAtMinutes is not { } minutes)
        {
            return false;
        }

        // stay offset-aware, a bare DateTime vs LocalDateTime reconverts the zone and breaks the dedupe off UTC
        var scheduled = new DateTimeOffset(localNow.Date.AddMinutes(minutes), localNow.Offset);
        var elapsed = localNow - scheduled;

        if (elapsed < TimeSpan.Zero || elapsed > Grace)
        {
            return false;
        }

        // once per occurrence, or a 5 minute tick restarts it for an hour
        return lastRunAt is not { } last || last < scheduled;
    }
}
