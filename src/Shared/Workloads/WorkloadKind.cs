namespace FifthBox.ServerManager.Shared.Workloads;

/// What a workload is: a container image (swarm) or a native executable/command (agent). Derived from
/// the target — Swarm ⇒ Container, Agent ⇒ Native.
public enum WorkloadKind
{
    Container,
    Native,
}
