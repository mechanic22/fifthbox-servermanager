namespace FifthBox.ServerManager.Shared.Workloads;

/// only way to tell "someone stopped it" from "it went missing"
public enum WorkloadDesiredState
{
    Running,
    Stopped,
}
