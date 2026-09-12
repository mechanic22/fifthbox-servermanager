namespace FifthBox.ServerManager.Shared.Workloads;

/// A published port: Published (reachable on the host / swarm ingress) → Target (the container's port).
/// Mode picks ingress (routing mesh) vs host (bound on the node).
public record PortMapping(int Published, int Target, PortProtocol Protocol, PortPublishMode Mode = PortPublishMode.Ingress);
