using Microsoft.AspNetCore.SignalR.Client;

namespace FifthBox.ServerManager.Agent;

/// Reconnect for as long as the agent is running.
///
/// The default policy gives up after four attempts spanning about 42 seconds, which is shorter than a
/// Host upgrade — and an agent that has quietly stopped trying looks exactly like one that is fine.
/// A refused credential is the only thing that should stop us, and that arrives as a close the connection
/// won't retry anyway.
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    /// Fast at first so a Host restart is barely noticed, then backing off to a steady poll so a Host
    /// that's down for an hour isn't hammered by every agent it has.
    private static readonly TimeSpan[] Ramp =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => DelayFor(retryContext.PreviousRetryCount);

    /// Never null: null is how a policy says "give up".
    public static TimeSpan DelayFor(long previousRetryCount) =>
        Ramp[(int)Math.Min(previousRetryCount, Ramp.Length - 1)];
}
