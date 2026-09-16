using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public enum RolloutOutcome
{
    /// still in flight, or nothing to judge yet
    Unsettled,

    Applied,

    Reverted,
}

public static class Rollout
{
    public static RolloutOutcome Of(WorkloadRuntimeStatus status) => status switch
    {
        { Deployed: false } => RolloutOutcome.Unsettled,
        { UpdateState: "rollback_completed" } => RolloutOutcome.Reverted,

        // a fresh service has no UpdateStatus until something updates it, so absent means applied
        // applied isn't healthy though, a broken first deploy is applied and failing
        { UpdateState: null or "" or "completed" } => RolloutOutcome.Applied,

        // updating, rollback_started, paused, ask again next time
        _ => RolloutOutcome.Unsettled,
    };

    /// swarm holds a paused update until someone deploys again
    public static bool Stalled(string? updateState) => updateState is "paused" or "rollback_paused";
}
