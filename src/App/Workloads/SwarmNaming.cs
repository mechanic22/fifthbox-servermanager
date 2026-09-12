namespace FifthBox.ServerManager.App.Workloads;

/// Namespaces the Docker objects a workload owns. Two reasons this isn't cosmetic: the swarm backend
/// deploys by *updating* a service it finds under the same name, so an unnamespaced workload could
/// silently take over something else on a shared daemon; and two workloads naming the same volume would
/// otherwise share one.
public static class SwarmNaming
{
    /// Double dash on purpose. A single one would let a workload called "nginx" or "host" render as
    /// fbsm-nginx / fbsm-host — the platform's own services — and a slug can never contain "--".
    public const string WorkloadPrefix = "fbsm--";

    public static string ServiceName(string workloadName) => $"{WorkloadPrefix}{workloadName}";

    /// Reverse of ServiceName, for reading docker events back. False for anything unprefixed — the
    /// platform's own services and any hand-created service on a shared daemon.
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

    /// Named volumes are scoped to the service that mounts them. Bind mounts are left alone: a host
    /// path is the deliberate way to share data between workloads.
    public static string VolumeName(string serviceName, string source) => $"{serviceName}--{source}";
}
