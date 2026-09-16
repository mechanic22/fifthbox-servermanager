namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// 0 means no limit, sent as null
public static class ResourceScale
{
    private const string NoLimit = "No limit";

    public static readonly int[] MemoryMb =
    [
        0, 64, 128, 256, 512, 768,
        1024, 1536, 2048, 3072, 4096, 6144, 8192, 12288, 16384, 24576, 32768, 49152, 65536,
    ];

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

    /// a ceiling above 0 caps the stops, a reservation can't go over its limit
    public static List<int> MemoryOptions(int current, int ceiling = 0) =>
        Splice([.. MemoryMb.Where(v => ceiling <= 0 || v <= ceiling)], current);

    public static List<double> CpuOptions(double current, double ceiling = 0) =>
        Splice([.. CpuCores.Where(v => ceiling <= 0 || v <= ceiling)], current);

    /// keeps an off-scale saved value so just opening Config doesn't change the workload
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
