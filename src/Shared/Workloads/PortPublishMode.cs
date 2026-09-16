namespace FifthBox.ServerManager.Shared.Workloads;

/// Host is needed on Docker/Rancher Desktop, the ingress mesh isn't reachable from the host there
public enum PortPublishMode
{
    Ingress,
    Host,
}
