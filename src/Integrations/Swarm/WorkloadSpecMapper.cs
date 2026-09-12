using System.Security.Cryptography;
using System.Text;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// A config object the backend has already created, ready to mount into the service.
public record ResolvedConfig(string Id, string Name, string Path);

/// A swarm secret the backend has already created. FileName is what it lands as under /run/secrets.
public record ResolvedSecret(string Id, string Name, string FileName);

/// Pure translation between our workload model and Docker's swarm service spec, plus runtime-state
/// derivation and config-object naming. The tested core of the backend; SwarmBackend wires the Docker
/// calls (create config objects, resolve the local node id) and passes the results in here.
public static class WorkloadSpecMapper
{
    public static ServiceSpec ToServiceSpec(
        WorkloadDeployment d,
        string? networkId,
        string? pinnedNodeId = null,
        IReadOnlyList<ResolvedConfig>? configs = null,
        IReadOnlyList<ResolvedSecret>? secrets = null)
    {
        var ports = d.Ports.SelectMany(ToPortConfigs).ToList();

        var containerSpec = new ContainerSpec
        {
            Image = d.Image,
            Env = d.Env.Select(e => $"{e.Key}={e.Value}").ToList(),
        };

        if (!string.IsNullOrWhiteSpace(d.HealthCommand))
        {
            containerSpec.Healthcheck = new HealthConfig
            {
                Test = ["CMD-SHELL", d.HealthCommand],
                Interval = TimeSpan.FromSeconds(d.HealthIntervalSeconds),
                Timeout = TimeSpan.FromSeconds(d.HealthTimeoutSeconds),
                Retries = d.HealthRetries,
                StartPeriod = (long)TimeSpan.FromSeconds(d.HealthStartPeriodSeconds).TotalNanoseconds,
            };
        }

        if (d.Mounts.Count > 0)
        {
            containerSpec.Mounts = d.Mounts.Select(m => new Mount
            {
                Type = m.Type == VolumeMountType.Bind ? "bind" : "volume",
                // Named volumes are scoped to this service so two workloads asking for "data" get their
                // own. A bind mount is a host path the operator chose — never rewritten.
                Source = m.Type == VolumeMountType.Bind ? m.Source : SwarmNaming.VolumeName(d.Name, m.Source),
                Target = m.Target,
                ReadOnly = m.ReadOnly,
            }).ToList();
        }

        if (configs is { Count: > 0 })
        {
            containerSpec.Configs = configs.Select(c => new SwarmConfigReference
            {
                ConfigID = c.Id,
                ConfigName = c.Name,
                File = new ConfigReferenceFileTarget { Name = c.Path, UID = "0", GID = "0", Mode = 0b100_100_100 }, // 0444
            }).ToList();
        }

        if (secrets is { Count: > 0 })
        {
            containerSpec.Secrets = secrets.Select(s => new SecretReference
            {
                SecretID = s.Id,
                SecretName = s.Name,
                // 0400: a private key readable by anything else in the container is a private key you
                // have to assume leaked.
                File = new SecretReferenceFileTarget { Name = s.FileName, UID = "0", GID = "0", Mode = 0b100_000_000 },
            }).ToList();
        }

        var task = new TaskSpec
        {
            ContainerSpec = containerSpec,
            Networks = networkId is null ? [] : [new NetworkAttachmentConfig { Target = networkId }],
        };

        var hasLimit = d.MemoryLimitMb is > 0 || d.CpuLimit is > 0;
        var hasReserve = d.MemoryReserveMb is > 0 || d.CpuReserve is > 0;

        if (hasLimit || hasReserve)
        {
            task.Resources = new ResourceRequirements
            {
                // Limits cap what a running container may use. Reservations are what the scheduler
                // subtracts from a node's capacity when deciding where the task fits — without them
                // every node reads as free and swarm overcommits.
                Limits = hasLimit ? new SwarmLimit
                {
                    MemoryBytes = ToBytes(d.MemoryLimitMb),
                    NanoCPUs = ToNanoCpus(d.CpuLimit),
                } : null,
                Reservations = hasReserve ? new SwarmResources
                {
                    MemoryBytes = ToBytes(d.MemoryReserveMb),
                    NanoCPUs = ToNanoCpus(d.CpuReserve),
                } : null,
            };
        }

        var constraints = ToConstraints(d, pinnedNodeId);
        if (constraints.Count > 0)
        {
            task.Placement = new Placement { Constraints = constraints };
        }

        task.RestartPolicy = new SwarmRestartPolicy
        {
            Condition = d.RestartPolicy switch
            {
                Shared.Workloads.RestartPolicy.Never => "none",
                Shared.Workloads.RestartPolicy.OnFailure => "on-failure",
                _ => "any",
            },
        };

        return new ServiceSpec
        {
            Name = d.Name,
            Labels = d.Labels is { Count: > 0 } ? new Dictionary<string, string>(d.Labels) : null,
            TaskTemplate = task,
            Mode = d.Mode == WorkloadMode.Global
                ? new ServiceMode { Global = new GlobalService() }
                : new ServiceMode { Replicated = new ReplicatedService { Replicas = (ulong)d.Replicas } },
            EndpointSpec = new EndpointSpec { Ports = ports },
            UpdateConfig = ToUpdateConfig(d),
            RollbackConfig = new SwarmUpdateConfig { Parallelism = 1, Order = "stop-first", FailureAction = "pause" },
        };
    }

    /// Swarm only recreates tasks when the *task* template changes, and published ports live outside it
    /// in the EndpointSpec. That's fine for ingress ports — the routing mesh reprograms itself — but a
    /// host-mode port is bound by the container, so changing one updates the spec and leaves the running
    /// container on the old binding until something else happens to restart it. Bumping the force-update
    /// counter is what makes the port change actually reach the host.
    public static ulong ForceUpdateFor(ServiceSpec? live, ServiceSpec desired, IEnumerable<TaskResponse>? tasks = null)
    {
        var current = live?.TaskTemplate?.ForceUpdate ?? 0;
        var wanted = PortKeys(desired.EndpointSpec?.Ports);

        // The spec catches a port we're about to change; the running tasks catch one that was changed
        // before this was here and never reached the container, which the spec alone reads as in sync.
        var running = RunningPortKeys(tasks);
        var stale = !wanted.SetEquals(PortKeys(live?.EndpointSpec?.Ports))
            || (running.Count > 0 && !wanted.SetEquals(running));

        return stale ? current + 1 : current;
    }

    private static HashSet<string> RunningPortKeys(IEnumerable<TaskResponse>? tasks)
        => PortKeys(tasks?
            .Where(t => t.Status?.State == TaskState.Running && t.Status.PortStatus?.Ports is not null)
            .SelectMany(t => t.Status.PortStatus.Ports));

    /// Mode included: swapping a port from ingress to host publishes it somewhere else entirely.
    private static HashSet<string> PortKeys(IEnumerable<PortConfig>? ports)
        => (ports ?? [])
            .Where(p => p.PublishedPort > 0)
            .Select(p => $"{p.PublishedPort}:{p.TargetPort}/{p.Protocol ?? "tcp"}/{p.PublishMode ?? "ingress"}")
            .ToHashSet(StringComparer.Ordinal);

    private static long ToBytes(int? megabytes) => megabytes is > 0 ? (long)megabytes.Value * 1024 * 1024 : 0;

    private static long ToNanoCpus(double? cores) => cores is > 0 ? (long)(cores.Value * 1_000_000_000) : 0;

    /// Where this may run: the pin if there is one, and always linux — swarm would otherwise place a
    /// container on a Windows node and let it fail there. Windows machines run the agent in this
    /// architecture rather than joining the swarm, so there is nothing to ask.
    public static List<string> ToConstraints(WorkloadDeployment d, string? pinnedNodeId)
    {
        var constraints = new List<string> { "node.platform.os == linux" };

        if (pinnedNodeId is not null)
        {
            constraints.Add($"node.id == {pinnedNodeId}");
        }

        return constraints;
    }

    /// How a redeploy is rolled out. With a health check, swarm watches the new task for Monitor and puts
    /// the old config back if it never turns healthy.
    public static SwarmUpdateConfig ToUpdateConfig(WorkloadDeployment d) => new()
    {
        Parallelism = 1,
        Order = UpdateOrder(d),
        FailureAction = string.IsNullOrWhiteSpace(d.HealthCommand) ? "pause" : "rollback",
        MaxFailureRatio = 0,
        Monitor = (long)TimeSpan
            .FromSeconds(d.HealthStartPeriodSeconds + (d.HealthIntervalSeconds * d.HealthRetries))
            .TotalNanoseconds,
    };

    /// start-first needs the old and new task alive at once, and three things make that impossible: a
    /// host-bound port the second one can't bind, and a mount — there is one copy of that data, and a
    /// second container opening it is how a database refuses to start (or worse, doesn't). Those
    /// workloads take the brief outage instead of failing to deploy.
    private static string UpdateOrder(WorkloadDeployment d) =>
        d.PinToControlNode || d.Mounts.Count > 0 || d.Ports.Any(p => p.Mode == PortPublishMode.Host)
            ? "stop-first"
            : "start-first";

    /// A global service's desired count is the cluster's own — swarm reports no replica number for one,
    /// so the tasks it still wants running are the only thing that answers "how many should there be".
    public static int DesiredCount(ServiceSpec? spec, IEnumerable<TaskResponse> tasks) =>
        spec?.Mode?.Global is not null
            ? tasks.Count(t => t.DesiredState == TaskState.Running)
            : (int)(spec?.Mode?.Replicated?.Replicas ?? 0);

    /// One PortConfig per protocol — Both expands to a tcp and a udp entry on the same published/target port.
    private static IEnumerable<PortConfig> ToPortConfigs(PortMapping p)
    {
        var mode = p.Mode == PortPublishMode.Host ? "host" : "ingress";
        var protocols = p.Protocol switch
        {
            PortProtocol.Both => new[] { "tcp", "udp" },
            PortProtocol.Udp => new[] { "udp" },
            _ => new[] { "tcp" },
        };

        return protocols.Select(proto => new PortConfig
        {
            PublishMode = mode,
            Protocol = proto,
            TargetPort = (uint)p.Target,
            PublishedPort = (uint)p.Published,
        });
    }

    /// Deterministic config-object name: same content → same name → redeploy is a no-op; changed content
    /// → new name → the service updates (rolling) and the old object is pruned.
    public static string ConfigObjectName(string serviceName, string configName, string content)
        => $"{serviceName}-{configName}-{ContentHash(content)}";

    /// Same scheme as config objects — swarm secrets are immutable too, so a changed key means a new
    /// object and a rolling update rather than an in-place edit.
    public static string SecretObjectName(string serviceName, string secretName, string content)
        => $"{serviceName}-{secretName}-{ContentHash(content)}";

    private static string ContentHash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..12].ToLowerInvariant();

    public static WorkloadRuntimeStatus ToRuntimeStatus(string name, bool deployed, int desired, int running) => new()
    {
        Name = name,
        Deployed = deployed,
        DesiredReplicas = desired,
        RunningReplicas = running,
        State = DeriveState(deployed, desired, running),
    };

    private static WorkloadState DeriveState(bool deployed, int desired, int running)
    {
        if (!deployed)
        {
            return WorkloadState.NotDeployed;
        }

        if (desired == 0)
        {
            return WorkloadState.Stopped;
        }

        return running >= desired ? WorkloadState.Running : WorkloadState.Partial;
    }
}
