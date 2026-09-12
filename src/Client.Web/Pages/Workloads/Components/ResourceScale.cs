namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// The values a workload's memory and CPU may take. Free text let people ask for 7 MB or 300 cores and
/// find out from the server; a fixed scale can only produce numbers that mean something. 0 is "no
/// limit" — the wire carries that as null.
public static class ResourceScale
{
    private const string NoLimit = "No limit";

    public static readonly int[] MemoryMb =
    [
        0, 64, 128, 256, 512, 768,
        1024, 1536, 2048, 3072, 4096, 6144, 8192, 12288, 16384, 24576, 32768, 49152, 65536,
    ];

    // Tenths where the difference matters, coarser once a workload is asking for whole cores.
    public static readonly double[] CpuCores =
    [
        0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9,
        1, 1.5, 2, 3, 4, 6, 8, 12, 16,
    ];

    public static string MemoryLabel(int mb) => mb switch
    {
        <= 0 => NoLimit,
        < 1024 => $"{mb} MB",
        _ => $"{Trim(mb / 1024d)} GB",
    };

    public static string CpuLabel(double cores) => cores switch
    {
        <= 0 => NoLimit,
        1 => "1 core",
        _ => $"{Trim(cores)} cores",
    };

    /// The stops on offer. A ceiling above 0 caps them — a reservation can't exceed its limit, and the
    /// server rejects one that does.
    public static List<int> MemoryOptions(int current, int ceiling = 0) =>
        Splice([.. MemoryMb.Where(v => ceiling <= 0 || v <= ceiling)], current);

    public static List<double> CpuOptions(double current, double ceiling = 0) =>
        Splice([.. CpuCores.Where(v => ceiling <= 0 || v <= ceiling)], current);

    /// A workload saved before this scale existed can hold a value that isn't on it. Rounding it to the
    /// nearest stop would change the workload just by opening its Config tab, so it keeps its own value
    /// until someone picks another.
    private static List<T> Splice<T>(List<T> options, T current) where T : struct, IComparable<T>
    {
        if (current.CompareTo(default) > 0 && !options.Contains(current))
        {
            options.Add(current);
            options.Sort();
        }

        return options;
    }

    private static string Trim(double value) => value.ToString("0.##");
}
