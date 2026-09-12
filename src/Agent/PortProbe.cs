using System.Net.Sockets;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

/// Whether anything is actually accepting connections on a workload's declared ports. "The process
/// exists" is a weak claim for a game server that has wedged with its socket closed.
public static class PortProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(400);

    /// Null when there is nothing to probe. UDP can't be checked by connecting — an unanswered datagram
    /// is indistinguishable from a healthy silent server — and plenty of game servers are UDP-only, so
    /// saying nothing beats reporting a false negative.
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
