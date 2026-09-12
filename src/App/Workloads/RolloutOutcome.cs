using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public enum RolloutOutcome
{
    /// Still in flight, or nothing to judge yet.
    Unsettled,

    /// The backend is holding this spec.
    Applied,

    /// The backend took the spec and then put the previous one back.
    Reverted,
}

/// Reads a runtime status for what the backend did with the last spec we sent it.
public static class Rollout
{
    public static RolloutOutcome Of(WorkloadRuntimeStatus status) => status switch
    {
        { Deployed: false } => RolloutOutcome.Unsettled,
        { UpdateState: "rollback_completed" } => RolloutOutcome.Reverted,

        // A freshly created service has no UpdateStatus at all — swarm only writes one once something
        // has updated it. Absent means nothing has overturned the spec, which is the same answer as
        // "completed". Note this says swarm is holding the spec, not that the container is healthy;
        // a first deploy of a broken image is applied and failing, and the task list says so.
        { UpdateState: null or "" or "completed" } => RolloutOutcome.Applied,

        // "updating", "rollback_started", "paused" — ask again next time.
        _ => RolloutOutcome.Unsettled,
    };

    /// Unsettled and waiting for nothing: swarm holds a paused update until someone deploys again.
    public static bool Stalled(string? updateState) => updateState is "paused" or "rollback_paused";
}
