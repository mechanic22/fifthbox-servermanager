namespace FifthBox.ServerManager.Shared.Routes;

public enum RouteTarget
{
    /// swarm service by name over the overlay
    Workload,

    /// anything the nginx container can reach, another box, a NAS, an agent's web port
    External,
}
