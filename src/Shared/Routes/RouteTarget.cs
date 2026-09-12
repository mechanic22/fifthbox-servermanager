namespace FifthBox.ServerManager.Shared.Routes;

/// What a route proxies to.
public enum RouteTarget
{
    /// A container workload on the swarm, reached by service name over the overlay.
    Workload,

    /// Anything else reachable from the nginx container — another box, a NAS, an agent's web port.
    External,
}
