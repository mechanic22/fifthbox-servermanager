namespace FifthBox.ServerManager.Shared.Workloads;

/// How a published port is exposed. Ingress = the swarm's routing mesh (load-balanced across nodes).
/// Host = bound directly on the node running the task (needed for direct host access on Docker/Rancher
/// Desktop, where the ingress mesh isn't reachable from the host).
public enum PortPublishMode
{
    Ingress,
    Host,
}
