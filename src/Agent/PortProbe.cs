using System.Net.Sockets;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

/// a wedged game server can be alive with its socket closed
public static class PortProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(400);

    /// null with no tcp ports. udp can't be probed, and saying nothing beats a false negative
    public static async Task<bool?> ReachableAsync(IReadOnlyList<PortMapping> ports, CancellationToken ct)
    {
        var tcp = ports.Where(p => p.Protocol is PortProtocol.Tcp or PortProtocol.Both).ToList();

        if (tcp.Count == 0)
        {
            return null;
        }

        foreach (var port in tcp)
        {
            if (!await ConnectsAsync(port.Published, ct))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ConnectsAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            await client.ConnectAsync("127.0.0.1", port, timeout.Token);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
