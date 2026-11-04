using System.Globalization;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Stats;

public static class StatsCalculator
{
    /// <param name="hostMemoryBytes">docker reports the machine's memory as the limit when there isn't one</param>
    public static ContainerStatsEntry ToEntry(string taskId, DockerStatsSample sample, long? hostMemoryBytes)
    {
        var limit = sample.Memory?.Limit ?? 0;

        return new ContainerStatsEntry
        {
            TaskId = taskId,
            CpuPercent = CpuPercent(sample),
            MemoryBytes = MemoryUsed(sample.Memory),
            MemoryLimitBytes = limit > 0 && limit < hostMemoryBytes ? limit : null,
        };
    }

    /// same sum as docker stats, 100 is one core
    public static double CpuPercent(DockerStatsSample sample)
    {
        var cpu = sample.Cpu?.Usage?.Total ?? 0;
        var previousCpu = sample.PreviousCpu?.Usage?.Total ?? 0;
        var system = sample.Cpu?.SystemUsage ?? 0;
        var previousSystem = sample.PreviousCpu?.SystemUsage ?? 0;

        // a restart resets the counters, and they're unsigned
        if (cpu <= previousCpu || system <= previousSystem)
        {
            return 0;
        }

        var cores = sample.Cpu?.OnlineCpus is > 0 and var online ? online : sample.Cpu?.Usage?.PerCpu?.Count ?? 1;
        return Math.Round((double)(cpu - previousCpu) / (system - previousSystem) * cores * 100, 2);
    }

    /// page cache doesn't count, same as docker stats. the name differs on cgroup v1 and v2
    public static long MemoryUsed(MemorySample? memory)
    {
        if (memory is null)
        {
            return 0;
        }

        var cache = memory.Stats?.GetValueOrDefault("inactive_file")
            ?? 0;
        if (cache == 0)
        {
            cache = memory.Stats?.GetValueOrDefault("total_inactive_file") ?? 0;
        }

        return Math.Max(0, memory.Usage - cache);
    }

    /// a /proc/meminfo value in bytes, null when it can't be read
    public static long? MemInfoBytes(string meminfo, string key)
    {
        foreach (var line in meminfo.Split('\n'))
        {
            if (!line.StartsWith($"{key}:", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kb)
                ? kb * 1024
                : null;
        }

        return null;
    }

    /// the first line of /proc/stat. idle includes iowait, a disk wait isn't cpu work
    public static CpuTicks? ParseCpuTicks(string stat)
    {
        var line = stat.Split('\n')[0];
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6 || parts[0] != "cpu")
        {
            return null;
        }

        var values = new List<ulong>();
        foreach (var part in parts.Skip(1).Take(8))
        {
            if (!ulong.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            values.Add(value);
        }

        return new CpuTicks(values.Aggregate(0UL, (sum, v) => sum + v), values[3] + values[4]);
    }

    /// from /proc/stat, Environment.ProcessorCount would only see this container's cpu limit
    public static int? CountCores(string stat)
    {
        var cores = stat.Split('\n').Count(line => line.StartsWith("cpu", StringComparison.Ordinal) && line.Length > 3 && char.IsAsciiDigit(line[3]));
        return cores > 0 ? cores : null;
    }

    public static double MachineCpuPercent(CpuTicks previous, CpuTicks current)
    {
        if (current.Total <= previous.Total || current.Idle < previous.Idle)
        {
            return 0;
        }

        var total = current.Total - previous.Total;
        var idle = current.Idle - previous.Idle;
        return Math.Clamp(Math.Round((total - Math.Min(idle, total)) / (double)total * 100, 2), 0, 100);
    }
}

public readonly record struct CpuTicks(ulong Total, ulong Idle);
