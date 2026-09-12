namespace FifthBox.ServerManager.Shared.Workloads;

public enum PortProtocol
{
    Tcp,
    Udp,

    /// Publishes the port on both TCP and UDP (expanded into two swarm PortConfigs on deploy).
    Both,
}
