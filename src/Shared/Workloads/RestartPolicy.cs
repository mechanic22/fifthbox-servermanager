namespace FifthBox.ServerManager.Shared.Workloads;

/// What an agent does when a native workload's process exits on its own. Container workloads ignore this
/// — swarm restarts failed tasks itself.
public enum RestartPolicy
{
    Never,
    OnFailure,
    Always,
}
