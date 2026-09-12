using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// The rules a workload's saved config has to satisfy, and the normalising that goes with them. Pure and
/// dependency-free, which is why it lives apart from the service that applies it.
internal static class WorkloadValidation
{
    public static string ValidateName(string name)
    {
        var slug = Slug.Make(name ?? string.Empty);
        if (string.IsNullOrEmpty(slug))
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Name), "A valid name is required.");
        }

        return slug;
    }
    public static string ValidateImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Image), "An image is required.");
        }

        return image.Trim();
    }
    public static string ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Command), "A command is required.");
        }

        return command.Trim();
    }
    public static int ValidateStopGrace(int seconds)
    {
        if (seconds is < 0 or > 300)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.StopGraceSeconds), "Stop grace must be between 0 and 300 seconds.");
        }

        return seconds;
    }
    public static void ValidateReplicas(int replicas)
    {
        if (replicas < 0)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Replicas), "Replicas cannot be negative.");
        }
    }
    public static void ValidateResources(int? memoryLimitMb, double? cpuLimit, int? memoryReserveMb, double? cpuReserve)
    {
        if (memoryLimitMb is < 0)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.MemoryLimitMb), "Memory limit cannot be negative.");
        }

        if (cpuLimit is < 0)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.CpuLimit), "CPU limit cannot be negative.");
        }

        if (memoryReserveMb is < 0)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.MemoryReserveMb), "Memory reservation cannot be negative.");
        }

        if (cpuReserve is < 0)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.CpuReserve), "CPU reservation cannot be negative.");
        }

        // Reserving more than the limit schedules space the container is then forbidden to use.
        if (memoryReserveMb > memoryLimitMb)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.MemoryReserveMb),
                "Memory reservation cannot exceed the memory limit.");
        }

        if (cpuReserve > cpuLimit)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.CpuReserve),
                "CPU reservation cannot exceed the CPU limit.");
        }
    }
    public static void ValidateMounts(IReadOnlyList<VolumeMount> mounts)
    {
        foreach (var m in mounts)
        {
            if (string.IsNullOrWhiteSpace(m.Source) || string.IsNullOrWhiteSpace(m.Target))
            {
                throw new ValidationException(nameof(CreateWorkloadRequest.Mounts), "Each mount needs a source and a target path.");
            }
        }
    }
    public static void ValidatePorts(IReadOnlyList<PortMapping> ports)
    {
        foreach (var port in ports)
        {
            if (port.Published is < 1 or > 65535 || port.Target is < 1 or > 65535)
            {
                throw new ValidationException(nameof(CreateWorkloadRequest.Ports), "Ports must be between 1 and 65535.");
            }
        }
    }
    public static int? ValidateHttpPort(int? port)
    {
        if (port is not null and (< 1 or > 65535))
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.HttpPort), "The HTTP port must be between 1 and 65535.");
        }

        return port;
    }
    /// Internal means nothing is published — nginx reaches it over the overlay, and the route carries the
    /// target port. Published means bound on the chosen node; there is no per-port choice.
    /// Always host-bound, never the ingress mesh: the mesh SNATs, so the container would see the gateway
    /// instead of the real client. Swarm treats a host port as a node resource and won't put two tasks of
    /// one service on the same node, which is what spreads the replicas.
    public static List<PortMapping> PublishedPorts(IReadOnlyList<PortMapping> ports) =>
        [.. ports.Select(p => new PortMapping(p.Published, p.Target, p.Protocol, PortPublishMode.Host))];
    public static bool HasNamedVolume(IEnumerable<VolumeMount> mounts) =>
        mounts.Any(m => m.Type == VolumeMountType.Volume);
    public static bool SingleInstance(WorkloadPlacement placement, IEnumerable<VolumeMount> mounts) =>
        placement == WorkloadPlacement.Node || HasNamedVolume(mounts);
    /// The node the backend has to honour: the one an operator picked, or — for a workload whose volume
    /// ties it down — the one we watched it land on. Revisions saved before placement existed carry a
    /// node with no placement, and the coalesce is what still honours them on a rollback.
    public static string? EffectiveNode(Workload w, string? nodeId) => nodeId ?? w.PlacedNodeId;
    /// A native process binds a port on its host directly — there's no container to map into, so
    /// published and target are the same number and it's always host-bound.
    public static List<PortMapping> NativePorts(IReadOnlyList<PortMapping> ports) =>
        [.. ports.Select(p => new PortMapping(p.Published, p.Published, p.Protocol, PortPublishMode.Host))];
    public static void ApplyHealth(Workload w, string? command, int interval, int timeout, int retries, int startPeriod)
    {
        w.HealthCommand = Blank(command);
        if (w.HealthCommand is null)
        {
            return;
        }

        // Zeros would make swarm poll continuously and never let a container finish starting.
        w.HealthIntervalSeconds = Positive(interval, nameof(CreateWorkloadRequest.HealthIntervalSeconds));
        w.HealthTimeoutSeconds = Positive(timeout, nameof(CreateWorkloadRequest.HealthTimeoutSeconds));
        w.HealthRetries = Positive(retries, nameof(CreateWorkloadRequest.HealthRetries));

        if (startPeriod is < 0 or > 3600)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.HealthStartPeriodSeconds), "Start period must be between 0 and 3600 seconds.");
        }

        w.HealthStartPeriodSeconds = startPeriod;
    }
    public static int Positive(int value, string member) => value is > 0 and <= 3600
        ? value
        : throw new ValidationException(member, "Must be between 1 and 3600.");
    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// Minutes past midnight, so 0..1439. Null is "never", which is the default.
    public static int? ValidateRestartTime(int? minutes)
    {
        if (minutes is null)
        {
            return null;
        }

        if (minutes is < 0 or > 1439)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.RestartDailyAtMinutes),
                "Pick a time of day for the restart.");
        }

        return minutes;
    }
}
