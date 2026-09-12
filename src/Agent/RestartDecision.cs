using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

public readonly record struct RestartVerdict(bool Restart, TimeSpan Delay, string? Reason);

/// Decides whether a process that exited should be started again, and how long to wait first. Pure —
/// the caller owns the timer and the process.
public static class RestartDecision
{
    /// Give up after this many consecutive failures. Without a cap, a workload that crashes on startup
    /// restarts forever and buries the real error.
    public const int MaxAttempts = 10;

    /// A run that lasts this long counts as a recovery, ending the crash run that preceded it. Without
    /// it the budget is a lifetime total, so a workload that crashes once a fortnight quietly stops being
    /// restarted months later — the ten attempts having been spent on ten unrelated incidents.
    public static readonly TimeSpan SuccessWindow = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    /// Whether a run that lasted this long earns a fresh restart budget.
    public static bool Recovered(TimeSpan uptime) => uptime >= SuccessWindow;

    /// <param name="attempt">Restarts already made in this crash run — 0 for the first exit.</param>
    public static RestartVerdict Decide(RestartPolicy policy, int exitCode, bool stopRequested, int attempt)
    {
        if (stopRequested)
        {
            return new RestartVerdict(false, TimeSpan.Zero, "stopped by operator");
        }

        var wanted = policy switch
        {
            RestartPolicy.Always => true,
            RestartPolicy.OnFailure => exitCode != 0,
            _ => false,
        };

        if (!wanted)
        {
            return new RestartVerdict(false, TimeSpan.Zero, policy == RestartPolicy.Never ? "restart policy is Never" : "exited cleanly");
        }

        if (attempt >= MaxAttempts)
        {
            return new RestartVerdict(false, TimeSpan.Zero, $"restart limit reached ({MaxAttempts} attempts)");
        }

        return new RestartVerdict(true, BackoffFor(attempt), null);
    }

    private static TimeSpan BackoffFor(int attempt)
    {
        // 2s, 4s, 8s … capped. Shifting past 30 would overflow the multiplier.
        var factor = 1L << Math.Min(attempt, 30);
        var delay = BaseDelay * factor;
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
