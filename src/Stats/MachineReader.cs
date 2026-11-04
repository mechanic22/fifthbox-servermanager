using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Stats;

/// /proc shows the host's numbers from inside a container
public sealed class MachineReader(IOptions<StatsOptions> options, ILogger<MachineReader> log)
{
    private CpuTicks? _previous;

    /// null outside swarm, without a node id there's no row to attach it to
    public MachineStats? Read()
    {
        if (string.IsNullOrWhiteSpace(options.Value.NodeId))
        {
            return null;
        }

        var meminfo = File.ReadAllText("/proc/meminfo");
        var total = StatsCalculator.MemInfoBytes(meminfo, "MemTotal") ?? 0;
        var available = StatsCalculator.MemInfoBytes(meminfo, "MemAvailable") ?? total;
        var stat = File.ReadAllText("/proc/stat");
        var ticks = StatsCalculator.ParseCpuTicks(stat);
        var cpu = _previous is { } previous && ticks is { } current ? StatsCalculator.MachineCpuPercent(previous, current) : (double?)null;
        _previous = ticks;
        var (diskFree, diskTotal) = Disk();

        return new MachineStats
        {
            NodeId = options.Value.NodeId,
            CpuPercent = cpu,
            CpuCores = StatsCalculator.CountCores(stat),
            MemoryUsedBytes = Math.Max(0, total - available),
            MemoryTotalBytes = total,
            DiskFreeBytes = diskFree,
            DiskTotalBytes = diskTotal,
        };
    }

    // an empty volume, it sits on docker's drive so this reads that drive with no access to what's on it
    private (long? Free, long? Total) Disk()
    {
        try
        {
            var drive = new DriveInfo(options.Value.DiskProbePath);
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            log.LogDebug(ex, "Couldn't read disk space");
            return (null, null);
        }
    }
}
