using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Logging;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmBackend(
    IDockerClient client,
    IRegistryAuthResolver registryAuth,
    ILogger<SwarmBackend> logger) : IWorkloadBackend
{
    public WorkloadKind SupportedKind => WorkloadKind.Container;

    public async Task DeployAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var networkId = await ResolveNetworkIdAsync(deployment.Network, ct);
        // PinToControlNode is for our platform services, NodeId is the operator's pick
        var pinnedNodeId = deployment.PinToControlNode ? await LocalNodeIdAsync(ct) : deployment.NodeId;
        var configs = await EnsureConfigsAsync(deployment, ct);
        var secrets = await EnsureSecretsAsync(deployment, ct);
        var spec = WorkloadSpecMapper.ToServiceSpec(deployment, networkId, pinnedNodeId, configs, secrets);
        var auth = await ResolveAuthHeaderAsync(deployment.Image, ct);

        var existing = await FindServiceAsync(deployment.Name, ct);
        if (existing is null)
        {
            await client.Swarm.CreateServiceAsync(new ServiceCreateParameters { Service = spec, RegistryAuth = auth }, ct);
        }
        else
        {
            var inspected = await client.Swarm.InspectServiceAsync(existing.ID, ct);
            var tasks = await client.Tasks.ListAsync(new TasksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["service"] = new Dictionary<string, bool> { [existing.ID] = true },
                },
            }, ct);

            spec.TaskTemplate.ForceUpdate = WorkloadSpecMapper.ForceUpdateFor(inspected.Spec, spec, tasks);

            await client.Swarm.UpdateServiceAsync(existing.ID, new ServiceUpdateParameters
            {
                Version = (long)inspected.Version.Index,
                Service = spec,
                RegistryAuth = auth,
            }, ct);
        }

        await PruneStaleConfigsAsync(deployment, configs, ct);
        await PruneStaleSecretsAsync(deployment, secrets, ct);
    }

    /// null when no configured registry matches
    private async Task<AuthConfig?> ResolveAuthHeaderAsync(string image, CancellationToken ct)
    {
        var auth = await registryAuth.ResolveAsync(image, ct);
        return auth is null ? null : new AuthConfig
        {
            Username = auth.Username,
            Password = auth.Password,
            ServerAddress = auth.ServerAddress,
        };
    }

    public async Task ScaleAsync(WorkloadDeployment deployment, int replicas, CancellationToken ct = default)
    {
        if (deployment.Mode == WorkloadMode.Global)
        {
            throw new ConflictException("A global workload runs one copy per node; there is no replica count to set.");
        }

        var name = deployment.Name;
        var existing = await FindServiceAsync(name, ct)
            ?? throw new NotFoundException($"Workload '{name}' is not deployed.");

        var inspected = await client.Swarm.InspectServiceAsync(existing.ID, ct);
        var spec = inspected.Spec;
        spec.Mode.Replicated.Replicas = (ulong)replicas;

        await client.Swarm.UpdateServiceAsync(existing.ID, new ServiceUpdateParameters
        {
            Version = (long)inspected.Version.Index,
            Service = spec,
        }, ct);
    }

    public async Task RestartAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var existing = await FindServiceAsync(deployment.Name, ct)
            ?? throw new NotFoundException($"Workload '{deployment.Name}' is not deployed.");

        // force-update bump is the supported way to restart a service
        var inspected = await client.Swarm.InspectServiceAsync(existing.ID, ct);
        var spec = inspected.Spec;
        spec.TaskTemplate ??= new TaskSpec();
        spec.TaskTemplate.ForceUpdate += 1;

        await client.Swarm.UpdateServiceAsync(existing.ID, new ServiceUpdateParameters
        {
            Version = (long)inspected.Version.Index,
            Service = spec,
        }, ct);
    }

    /// scales to zero so the service keeps its id, ports, configs and task history
    /// global services can't, so those get removed and deploy recreates them
    public async Task StopAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        if (await FindServiceAsync(deployment.Name, ct) is null)
        {
            return;
        }

        if (deployment.Mode == WorkloadMode.Global)
        {
            await UndeployAsync(deployment, ct);
            return;
        }

        await ScaleAsync(deployment, 0, ct);
    }

    public async Task StartAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        // stop removes a global service, so starting one is a create
        if (await FindServiceAsync(deployment.Name, ct) is null)
        {
            await DeployAsync(deployment, ct);
            return;
        }

        if (deployment.Mode == WorkloadMode.Global)
        {
            return;
        }

        await ScaleAsync(deployment, deployment.Replicas, ct);
    }

    public async Task UndeployAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var existing = await FindServiceAsync(deployment.Name, ct);
        if (existing is not null)
        {
            await client.Swarm.RemoveServiceAsync(existing.ID, ct);
        }
    }

    public async Task<WorkloadRuntimeStatus> GetStatusAsync(WorkloadDeployment deployment, CancellationToken ct = default)
    {
        var name = deployment.Name;
        var existing = await FindServiceAsync(name, ct);
        if (existing is null)
        {
            return WorkloadSpecMapper.ToRuntimeStatus(name, deployed: false, desired: 0, running: 0);
        }

        var tasks = await client.Tasks.ListAsync(new TasksListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["service"] = new Dictionary<string, bool> { [existing.ID] = true },
            },
        }, ct);

        var desired = WorkloadSpecMapper.DesiredCount(existing.Spec, tasks);
        var running = tasks.Count(t => t.Status?.State == TaskState.Running);
        var taskHistory = SwarmTaskMapper.ToTasks(tasks);

        var inspected = await client.Swarm.InspectServiceAsync(existing.ID, ct);
        return WorkloadSpecMapper.ToRuntimeStatus(name, deployed: true, desired, running) with
        {
            UpdateState = inspected.UpdateStatus?.State,
            UpdateMessage = inspected.UpdateStatus?.Message,
            RolloutStalled = Rollout.Stalled(inspected.UpdateStatus?.State),
            Tasks = taskHistory,
            LastError = SwarmTaskMapper.LastError(taskHistory),
            RunningImage = existing.Spec?.TaskTemplate?.ContainerSpec?.Image,
        };
    }

    /// no-op, pulling the image on deploy is the update
    public Task UpdateAsync(WorkloadDeployment deployment, CancellationToken ct = default) => Task.CompletedTask;

    /// throws, a swarm task has no stdin the manager can reach
    public Task SendConsoleAsync(WorkloadDeployment deployment, string text, CancellationToken ct = default)
        => throw new ConflictException("Console input is only available for workloads that run on an agent.");

    public async Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(WorkloadDeployment deployment, int tail, CancellationToken ct = default)
    {
        var service = await FindServiceAsync(deployment.Name, ct);
        if (service is null)
        {
            return [];
        }

        var parameters = new ServiceLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Tail = tail.ToString(),
        };

        // ask about TTY, guessing the framing wrong throws "unknown stream type"
        var tty = service.Spec?.TaskTemplate?.ContainerSpec?.TTY ?? false;

        string stdout, stderr;
        try
        {
            using var stream = await client.Swarm.GetServiceLogsAsync(service.ID, tty, parameters, ct);
            (stdout, stderr) = await stream.ReadOutputToEndAsync(ct);
        }
        catch (Exception ex) when (ex is IOException or DockerApiException)
        {
            // no running task just means no logs yet, don't fail the Logs tab
            logger.LogDebug(ex, "No readable logs for service '{Service}'", deployment.Name);
            return [];
        }

        return [.. SwarmLogParser.Parse(stdout, LogStream.Stdout)
            .Concat(SwarmLogParser.Parse(stderr, LogStream.Stderr))
            .OrderBy(l => l.Timestamp ?? DateTimeOffset.MinValue)];
    }

    private async Task<string> LocalNodeIdAsync(CancellationToken ct)
        => (await client.System.GetSystemInfoAsync(ct)).Swarm.NodeID;

    /// idempotent by content hash
    private async Task<IReadOnlyList<ResolvedConfig>> EnsureConfigsAsync(WorkloadDeployment deployment, CancellationToken ct)
    {
        if (deployment.Configs.Count == 0)
        {
            return [];
        }

        var existing = await client.Configs.ListConfigsAsync(ct);
        var resolved = new List<ResolvedConfig>(deployment.Configs.Count);

        foreach (var mount in deployment.Configs)
        {
            var name = WorkloadSpecMapper.ConfigObjectName(deployment.Name, mount.Name, mount.Content);
            var id = existing.FirstOrDefault(c => c.Spec?.Name == name)?.ID
                ?? (await client.Configs.CreateConfigAsync(new SwarmCreateConfigParameters
                {
                    Config = new SwarmConfigSpec { Name = name, Data = Encoding.UTF8.GetBytes(mount.Content) },
                }, ct)).ID;

            resolved.Add(new ResolvedConfig(id, name, mount.Path));
        }

        return resolved;
    }

    private async Task<IReadOnlyList<ResolvedSecret>> EnsureSecretsAsync(WorkloadDeployment deployment, CancellationToken ct)
    {
        if (deployment.Secrets.Count == 0)
        {
            return [];
        }

        var existing = await client.Secrets.ListAsync(ct);
        var resolved = new List<ResolvedSecret>(deployment.Secrets.Count);

        foreach (var mount in deployment.Secrets)
        {
            var name = WorkloadSpecMapper.SecretObjectName(deployment.Name, mount.Name, mount.Content);
            var id = existing.FirstOrDefault(s => s.Spec?.Name == name)?.ID
                ?? (await client.Secrets.CreateAsync(new SecretSpec
                {
                    Name = name,
                    Data = Encoding.UTF8.GetBytes(mount.Content),
                }, ct)).ID;

            resolved.Add(new ResolvedSecret(id, name, mount.FileName));
        }

        return resolved;
    }

    private async Task PruneStaleSecretsAsync(WorkloadDeployment deployment, IReadOnlyList<ResolvedSecret> current, CancellationToken ct)
    {
        if (deployment.Secrets.Count == 0)
        {
            return;
        }

        var keep = current.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        var all = await client.Secrets.ListAsync(ct);

        foreach (var mount in deployment.Secrets)
        {
            var prefix = $"{deployment.Name}-{mount.Name}-";
            foreach (var stale in all.Where(s => s.Spec?.Name is { } n && n.StartsWith(prefix, StringComparison.Ordinal) && !keep.Contains(n)))
            {
                try
                {
                    await client.Secrets.DeleteAsync(stale.ID, ct);
                }
                catch (DockerApiException)
                {
                    // still held by a draining task, next apply gets it
                }
            }
        }
    }

    /// still-referenced ones are left for the next apply
    private async Task PruneStaleConfigsAsync(WorkloadDeployment deployment, IReadOnlyList<ResolvedConfig> current, CancellationToken ct)
    {
        if (deployment.Configs.Count == 0)
        {
            return;
        }

        var keep = current.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var all = await client.Configs.ListConfigsAsync(ct);

        foreach (var mount in deployment.Configs)
        {
            var prefix = $"{deployment.Name}-{mount.Name}-";
            foreach (var stale in all.Where(c => c.Spec?.Name is { } n && n.StartsWith(prefix, StringComparison.Ordinal) && !keep.Contains(n)))
            {
                try
                {
                    await client.Configs.RemoveConfigAsync(stale.ID, ct);
                }
                catch (DockerApiException)
                {
                    // still held by a draining task, next apply gets it
                }
            }
        }
    }

    private async Task<SwarmService?> FindServiceAsync(string name, CancellationToken ct)
    {
        // daemon name filter is a prefix match, so still check the exact name
        var services = await client.Swarm.ListServicesAsync(new ServicesListParameters
        {
            Filters = new ServiceFilter { Name = [name] },
        }, ct);

        return services.FirstOrDefault(s => string.Equals(s.Spec?.Name, name, StringComparison.Ordinal));
    }

    private async Task<string?> ResolveNetworkIdAsync(string? name, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var networks = await client.Networks.ListNetworksAsync(cancellationToken: ct);
        return networks.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.Ordinal))?.ID;
    }
}
