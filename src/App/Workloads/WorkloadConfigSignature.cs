using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

internal static class WorkloadConfigSignature
{
    public static string Of(Workload w) =>
        Build(w.Image, w.Mode, w.Replicas, w.Ports, w.Placement, w.NodeId, w.MemoryLimitMb, w.CpuLimit, w.MemoryReserveMb, w.CpuReserve, w.Mounts, w.Command, w.Args, w.WorkingDirectory,
            w.RestartPolicy, w.StopGraceSeconds, w.StopCommand, w.ManagedDirectory, w.Source, w.Env,
            w.HealthCommand, w.HealthIntervalSeconds, w.HealthTimeoutSeconds, w.HealthRetries, w.HealthStartPeriodSeconds);

    public static string Of(WorkloadRevision r) =>
        Build(r.Image, r.Mode, r.Replicas, r.Ports, r.Placement, r.NodeId, r.MemoryLimitMb, r.CpuLimit, r.MemoryReserveMb, r.CpuReserve, r.Mounts, r.Command, r.Args, r.WorkingDirectory,
            r.RestartPolicy, r.StopGraceSeconds, r.StopCommand, r.ManagedDirectory, r.Source, r.Env,
            r.HealthCommand, r.HealthIntervalSeconds, r.HealthTimeoutSeconds, r.HealthRetries, r.HealthStartPeriodSeconds);

    private static string Build(
        string? image, WorkloadMode mode, int replicas, IEnumerable<PortMapping> ports, WorkloadPlacement exposure, string? nodeId, int? memoryLimitMb, double? cpuLimit, int? memoryReserveMb, double? cpuReserve,
        IEnumerable<VolumeMount> mounts, string? command, IEnumerable<string> args, string? workingDir,
        RestartPolicy restartPolicy, int stopGraceSeconds, string? stopCommand, bool managedDirectory, WorkloadSource source, IEnumerable<EnvVar> env,
        string? healthCommand, int healthInterval, int healthTimeout, int healthRetries, int healthStartPeriod)
    {
        var portsPart = string.Join(",", ports
            .OrderBy(p => p.Published).ThenBy(p => p.Target).ThenBy(p => p.Protocol)
            .Select(p => $"{p.Published}:{p.Target}:{p.Protocol}:{p.Mode}"));
        var mountsPart = string.Join(",", mounts
            .OrderBy(m => m.Target, StringComparer.Ordinal).ThenBy(m => m.Source, StringComparer.Ordinal)
            .Select(m => $"{m.Type}:{m.Source}:{m.Target}:{m.ReadOnly}"));
        var envPart = string.Join(",", env
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => $"{e.Key}={e.Value}"));
        var argsPart = string.Join("", args); // positional, keep order

        // the ciphertext, not the secret, it's byte-stable for an unchanged password
        var sourcePart = $"{source.Kind}:{source.Url}:{source.SteamAppId}:{source.SteamBranch}:{source.SteamUsername}:{source.SteamPasswordEnc}";

        return $"img={image}|mode={mode}|rep={replicas}|ports={portsPart}|exp={exposure}|node={nodeId}|mem={memoryLimitMb}|cpu={cpuLimit}|memres={memoryReserveMb}|cpures={cpuReserve}|mounts={mountsPart}|cmd={command}|args={argsPart}|wd={workingDir}|restart={restartPolicy}|grace={stopGraceSeconds}|stopcmd={stopCommand}|managed={managedDirectory}|src={sourcePart}|env={envPart}|health={healthCommand}:{healthInterval}:{healthTimeout}:{healthRetries}:{healthStartPeriod}";
    }
}
