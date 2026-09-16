using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

public readonly record struct RestartVerdict(bool Restart, TimeSpan Delay, string? Reason);

public static class RestartDecision
{
    /// no cap and a crash-on-start restarts forever and buries the real error
    public const int MaxAttempts = 10;

    /// up this long resets the budget, otherwise rare crashes use it up over months
    public static readonly TimeSpan SuccessWindow = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    public static bool Recovered(TimeSpan uptime) => uptime >= SuccessWindow;

    /// attempt is restarts already made this crash run, 0 on the first exit
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
        // 2s, 4s, 8s... capped. shifting past 30 overflows
        var factor = 1L << Math.Min(attempt, 30);
        var delay = BaseDelay * factor;
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
