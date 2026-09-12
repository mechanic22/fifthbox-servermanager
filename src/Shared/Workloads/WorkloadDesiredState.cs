namespace FifthBox.ServerManager.Shared.Workloads;

/// What the operator last asked for, as distinct from what the backend currently reports. It is the only
/// thing that separates "someone stopped this" from "this was running and has gone missing".
public enum WorkloadDesiredState
{
    Running,
    Stopped,
}
