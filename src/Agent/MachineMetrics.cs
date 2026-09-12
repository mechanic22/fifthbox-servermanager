using System.Diagnostics;
using FifthBox.ServerManager.Shared.Agents;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

/// What the agent can say about its own machine without per-OS code. Disk is read for the drive holding
/// the workload root, because that is the one that fills up.
public sealed class MachineMetrics(IOptions<AgentOptions> options)
{
    private readonly string _root = options.Value.ResolvedRootPath;

    public AgentMetrics Read()
    {
        var (total, free) = Disk();

        return new AgentMetrics
        {
            DiskTotalBytes = total,
            DiskFreeBytes = free,
            ProcessorCount = Environment.ProcessorCount,
            AgentMemoryBytes = Process.GetCurrentProcess().WorkingSet64,
            ReportedAt = DateTimeOffset.UtcNow,
        };
    }

    private (long Total, long Free) Disk()
    {
        try
        {
            // The root may not exist until the first managed deploy, so walk up to something that does.
            var path = Path.GetFullPath(_root);
            while (!Directory.Exists(path) && Path.GetDirectoryName(path) is { Length: > 0 } parent)
            {
                path = parent;
            }

            var drive = new DriveInfo(Path.GetPathRoot(path) ?? path);
            return drive.IsReady ? (drive.TotalSize, drive.AvailableFreeSpace) : (0, 0);
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }
}
