using System.Net;
using System.Net.Sockets;
using FifthBox.ServerManager.Agent;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class PortProbeTests
{
    private static PortMapping Port(int published, PortProtocol protocol = PortProtocol.Tcp) =>
        new(published, published, protocol, PortPublishMode.Host);

    [TestMethod]
    public async Task Nothing_declared_means_nothing_to_say()
    {
        Assert.IsNull(await PortProbe.ReachableAsync([], CancellationToken.None));
    }

    [TestMethod]
    public async Task A_udp_only_workload_is_unknown_rather_than_unreachable()
    {
        // Plenty of game servers are UDP-only. An unanswered datagram looks exactly like a healthy quiet
        // server, so reporting false here would be a lie the operator would learn to ignore.
        Assert.IsNull(await PortProbe.ReachableAsync([Port(27015, PortProtocol.Udp)], CancellationToken.None));
    }

    [TestMethod]
    public async Task A_listening_port_is_reachable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Assert.IsTrue(await PortProbe.ReachableAsync([Port(port)], CancellationToken.None));
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public async Task A_closed_port_is_not_reachable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        Assert.IsFalse(await PortProbe.ReachableAsync([Port(port)], CancellationToken.None));
    }

    [TestMethod]
    public async Task Every_declared_tcp_port_has_to_answer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var open = ((IPEndPoint)listener.LocalEndpoint).Port;

        var closedListener = new TcpListener(IPAddress.Loopback, 0);
        closedListener.Start();
        var closed = ((IPEndPoint)closedListener.LocalEndpoint).Port;
        closedListener.Stop();

        try
        {
            Assert.IsFalse(await PortProbe.ReachableAsync([Port(open), Port(closed)], CancellationToken.None));
        }
        finally
        {
            listener.Stop();
        }
    }
}
