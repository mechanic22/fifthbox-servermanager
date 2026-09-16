using System.Diagnostics;
using FifthBox.ServerManager.Shared.Agents;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

/// disk is for the drive holding the workload root, that's the one that fills up
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
            // root may not exist before the first managed deploy, walk up until something does
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
