using Microsoft.AspNetCore.SignalR.Client;

namespace FifthBox.ServerManager.Agent;

/// default gives up after ~42s, shorter than a Host upgrade, and a quietly dead agent looks fine
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    /// fast for a Host restart, then back off so a long outage isn't hammered by every agent
    private static readonly TimeSpan[] Ramp =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext) => DelayFor(retryContext.PreviousRetryCount);

    /// never null, null means give up
    public static TimeSpan DelayFor(long previousRetryCount) =>
        Ramp[(int)Math.Min(previousRetryCount, Ramp.Length - 1)];
}
