using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Workloads;

/// Turns a stored workload into the shape a backend runs. One implementation on purpose: this used to be
/// duplicated in AgentDesiredState, and the copy there forgot to decrypt secret env — so a workload with
/// a secret got ciphertext, but only on the reconcile path.
public sealed class WorkloadDeploymentFactory(ISecretProtector protector, IOptions<ClusterOptions> clusterOptions)
{
    private readonly string _network = clusterOptions.Value.OverlayNetwork;

    public WorkloadDeployment ToDeployment(Workload w) => new()
    {
        Name = BackendName(w),
        Kind = w.Kind,
        AgentId = w.AgentId,
        Image = w.Image ?? string.Empty,
        Replicas = w.Replicas,
        Ports = w.Ports,
        NodeId = WorkloadValidation.EffectiveNode(w, w.NodeId),
        Mode = w.Mode,
        MemoryLimitMb = w.MemoryLimitMb,
        CpuLimit = w.CpuLimit,
        MemoryReserveMb = w.MemoryReserveMb,
        CpuReserve = w.CpuReserve,
        Mounts = w.Mounts,
        Network = _network,
        Command = w.Command,
        Args = w.Args,
        WorkingDirectory = w.WorkingDirectory,
        RestartPolicy = w.RestartPolicy,
        StopGraceSeconds = w.StopGraceSeconds,
        StopCommand = w.StopCommand,
        ManagedDirectory = w.ManagedDirectory,
        Source = SourceSpec(w.Source),
        Env = Decrypted(w.Env),
        HealthCommand = w.HealthCommand,
        HealthIntervalSeconds = w.HealthIntervalSeconds,
        HealthTimeoutSeconds = w.HealthTimeoutSeconds,
        HealthRetries = w.HealthRetries,
        HealthStartPeriodSeconds = w.HealthStartPeriodSeconds,
    };
    public WorkloadDeployment ToDeployment(Workload w, WorkloadRevision r) => new()
    {
        Name = BackendName(w),
        Kind = w.Kind,
        AgentId = w.AgentId,
        Image = r.Image ?? string.Empty,
        Replicas = r.Replicas,
        Ports = r.Ports,
        NodeId = WorkloadValidation.EffectiveNode(w, r.NodeId),
        Mode = r.Mode,
        MemoryLimitMb = r.MemoryLimitMb,
        CpuLimit = r.CpuLimit,
        MemoryReserveMb = r.MemoryReserveMb,
        CpuReserve = r.CpuReserve,
        Mounts = r.Mounts,
        Network = _network,
        Command = r.Command,
        Args = r.Args,
        WorkingDirectory = r.WorkingDirectory,
        RestartPolicy = r.RestartPolicy,
        StopGraceSeconds = r.StopGraceSeconds,
        StopCommand = r.StopCommand,
        ManagedDirectory = r.ManagedDirectory,
        Source = SourceSpec(r.Source),
        Env = Decrypted(r.Env),
        HealthCommand = r.HealthCommand,
        HealthIntervalSeconds = r.HealthIntervalSeconds,
        HealthTimeoutSeconds = r.HealthTimeoutSeconds,
        HealthRetries = r.HealthRetries,
        HealthStartPeriodSeconds = r.HealthStartPeriodSeconds,
    };
    public WorkloadSourceSpec? SourceSpec(WorkloadSource source) => source.Kind == SourceKind.None ? null : new WorkloadSourceSpec
    {
        Kind = source.Kind,
        Url = source.Url,
        SteamAppId = source.SteamAppId,
        SteamBranch = source.SteamBranch,
        SteamUsername = source.SteamUsername,
        SteamPassword = string.IsNullOrEmpty(source.SteamPasswordEnc) ? null : protector.Unprotect(source.SteamPasswordEnc),
    };
    public List<EnvVar> Decrypted(IEnumerable<EnvVar> env) =>
        env.Select(v => v.Secret ? v with { Value = protector.Unprotect(v.Value) } : v).ToList();
    /// What the backend knows this workload as. Swarm services are namespaced; an agent keeps tracking
    /// its processes by the plain name, and renaming those would orphan whatever is already running.
    public static string BackendName(Workload w) =>
        w.Kind == WorkloadKind.Container ? SwarmNaming.ServiceName(w.Name) : w.Name;
}
