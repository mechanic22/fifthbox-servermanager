namespace FifthBox.ServerManager.Shared.Workloads;

/// native only, swarm restarts containers itself
public enum RestartPolicy
{
    Never,
    OnFailure,
    Always,
}
