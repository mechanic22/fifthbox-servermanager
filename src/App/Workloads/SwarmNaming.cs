namespace FifthBox.ServerManager.App.Workloads;

/// deploy updates whatever service it finds by name, so without a prefix a workload could hijack
/// another service or share its volume
public static class SwarmNaming
{
    /// double dash so "nginx" can't render as fbsm-nginx, and a slug never contains "--"
    public const string WorkloadPrefix = "fbsm--";

    public static string ServiceName(string workloadName) => $"{WorkloadPrefix}{workloadName}";

    /// false for unprefixed services (the platform's own or hand-made ones)
    public static bool TryWorkloadName(string? serviceName, out string workloadName)
    {
        if (serviceName is not null
            && serviceName.StartsWith(WorkloadPrefix, StringComparison.Ordinal)
            && serviceName.Length > WorkloadPrefix.Length)
        {
            workloadName = serviceName[WorkloadPrefix.Length..];
            return true;
        }

        workloadName = string.Empty;
        return false;
    }

    /// named volumes are scoped per service, bind mounts left alone since sharing a host path is deliberate
    public static string VolumeName(string serviceName, string source) => $"{serviceName}--{source}";
}
