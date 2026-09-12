namespace FifthBox.ServerManager.Shared.Workloads;

/// Where a workload runs: as a service on the Docker Swarm, or as a native process on a custom agent.
public enum WorkloadTarget
{
    Swarm,
    Agent,
}
