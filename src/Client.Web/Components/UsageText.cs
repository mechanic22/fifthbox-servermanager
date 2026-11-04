using System.Globalization;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Components;

public static class UsageText
{
    private const long Mb = 1024 * 1024;
    private const long Gb = Mb * 1024;

    public static string Bytes(long bytes) => bytes >= Gb
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Gb:0.#} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Mb:0} MB");

    /// <param name="cpuLimit">cores, from the workload's saved config</param>
    public static string Replica(ContainerStatsEntry entry, double? cpuLimit = null)
    {
        var memory = entry.MemoryLimitBytes is { } limit
            ? $"{Bytes(entry.MemoryBytes)} / {Bytes(limit)}"
            : Bytes(entry.MemoryBytes);

        var cpu = cpuLimit is > 0 and var cores
            ? $"{Percent(entry.CpuPercent)} of {Cores(cores)}"
            : $"{Percent(entry.CpuPercent)} CPU";

        return $"{cpu} · {memory}";
    }

    /// "CPU of 32 cores", the percent means nothing without knowing what it's a share of
    public static string CpuTitle(double? cores) => cores is > 0 and var count ? $"CPU of {Cores(count)}" : "CPU";

    private static string Cores(double cores) => cores == 1
        ? "1 core"
        : string.Create(CultureInfo.InvariantCulture, $"{cores:0.##} cores");

    private static string Percent(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%");
}
