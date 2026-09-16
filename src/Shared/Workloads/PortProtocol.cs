namespace FifthBox.ServerManager.Shared.Workloads;

public enum PortProtocol
{
    Tcp,
    Udp,

    /// expands into two swarm port configs on deploy
    Both,
}
