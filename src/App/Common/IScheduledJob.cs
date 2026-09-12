namespace FifthBox.ServerManager.App.Common;

/// Recurring background work. Implementations declare how often they want to run; the composition root
/// owns the timer and the scoping, so a job never schedules or hosts itself.
public interface IScheduledJob
{
    /// Stable identifier — used in the API and the UI, so don't rename one casually.
    string Name { get; }

    TimeSpan Interval { get; }

    Task RunAsync(CancellationToken ct = default);
}
