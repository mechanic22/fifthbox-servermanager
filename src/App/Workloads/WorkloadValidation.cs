using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

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

        // reserving more than the limit schedules space the container can't use
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
    /// always host mode, the ingress mesh SNATs so the container would lose the client ip
    public static List<PortMapping> PublishedPorts(IReadOnlyList<PortMapping> ports) =>
        [.. ports.Select(p => new PortMapping(p.Published, p.Target, p.Protocol, PortPublishMode.Host))];
    public static bool HasNamedVolume(IEnumerable<VolumeMount> mounts) =>
        mounts.Any(m => m.Type == VolumeMountType.Volume);
    public static bool SingleInstance(WorkloadPlacement placement, IEnumerable<VolumeMount> mounts) =>
        placement == WorkloadPlacement.Node || HasNamedVolume(mounts);
    /// old revisions carry a node with no placement, the coalesce still honours them on rollback
    public static string? EffectiveNode(Workload w, string? nodeId) => nodeId ?? w.PlacedNodeId;
    /// published and target are the same, a process binds the host port directly
    public static List<PortMapping> NativePorts(IReadOnlyList<PortMapping> ports) =>
        [.. ports.Select(p => new PortMapping(p.Published, p.Published, p.Protocol, PortPublishMode.Host))];
    public static void ApplyHealth(Workload w, string? command, int interval, int timeout, int retries, int startPeriod)
    {
        w.HealthCommand = Blank(command);
        if (w.HealthCommand is null)
        {
            return;
        }

        // zeros make swarm poll nonstop and a container never finishes starting
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

    /// minutes past midnight (0..1439), null is never
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
