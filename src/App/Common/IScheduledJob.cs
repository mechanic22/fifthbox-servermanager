namespace FifthBox.ServerManager.App.Common;

public interface IScheduledJob
{
    /// shows up in the API and UI, don't rename casually
    string Name { get; }

    TimeSpan Interval { get; }

    Task RunAsync(CancellationToken ct = default);
}
