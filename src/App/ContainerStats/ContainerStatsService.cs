using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.ContainerStats;

public interface IContainerStatsService
{
    Task<ContainerStatsServiceResponse> GetAsync(CancellationToken ct = default);

    /// also how a changed key or spec reaches a running service, npm run deploy only moves the image
    Task<ContainerStatsServiceResponse> DeployAsync(CancellationToken ct = default);

    Task RemoveAsync(CancellationToken ct = default);
}

public sealed class ContainerStatsService(
    IWorkloadBackendResolver backends,
    IPlatformServices platform,
    IOptions<ContainerStatsOptions> options,
    IOptions<HostDeploymentOptions> host,
    IOptions<ClusterOptions> cluster) : IContainerStatsService
{
    private const string SecretName = "key";
    private const string SecretFile = "fbsm-stats-key";
    private const string DockerSocket = "/var/run/docker.sock";
    private const string DiskProbeVolume = "disk-probe";
    private const string DiskProbePath = "/var/fbsm-disk-probe";

    public async Task<ContainerStatsServiceResponse> GetAsync(CancellationToken ct = default)
    {
        var services = await platform.ListAsync(ct);
        var stats = services.FirstOrDefault(s => s.Name == options.Value.ServiceName);
        var hostVersion = TagOf(services.FirstOrDefault(s => s.Name == host.Value.ServiceName)?.Image);
        var version = TagOf(stats?.Image);

        return new ContainerStatsServiceResponse
        {
            Deployed = stats is not null,
            CanDeploy = !string.IsNullOrEmpty(options.Value.SigningKey),
            State = stats?.State ?? WorkloadState.NotDeployed,
            RunningReplicas = stats?.RunningReplicas ?? 0,
            DesiredReplicas = stats?.DesiredReplicas ?? 0,
            Version = version,
            HostVersion = hostVersion,
            BehindHost = version is not null && hostVersion is not null && hostVersion != "latest" && version != hostVersion,
        };
    }

    public async Task<ContainerStatsServiceResponse> DeployAsync(CancellationToken ct = default)
    {
        if (options.Value.SigningKey is not { Length: > 0 } key)
        {
            throw new ConflictException("This Host is running on a generated key. Configure Platform:Encryption:Key first.");
        }

        var services = await platform.ListAsync(ct);
        var hostVersion = TagOf(services.FirstOrDefault(s => s.Name == host.Value.ServiceName)?.Image);

        await backends.Resolve(WorkloadKind.Container).DeployAsync(BuildDeployment(key, hostVersion ?? "latest"), ct);
        return await GetAsync(ct);
    }

    public Task RemoveAsync(CancellationToken ct = default) =>
        backends.Resolve(WorkloadKind.Container).UndeployAsync(BuildDeployment(options.Value.SigningKey ?? string.Empty, "latest"), ct);

    public WorkloadDeployment BuildDeployment(string key, string version) => new()
    {
        Name = options.Value.ServiceName,
        Kind = WorkloadKind.Container,
        Image = $"{options.Value.Image}:{version}",
        Mode = WorkloadMode.Global,
        Network = cluster.Value.OverlayNetwork,
        Labels = PlatformLabels.Marker,
        RestartPolicy = RestartPolicy.Always,
        Mounts =
        [
            new VolumeMount(VolumeMountType.Bind, DockerSocket, DockerSocket, ReadOnly: true),
            new VolumeMount(VolumeMountType.Volume, DiskProbeVolume, DiskProbePath, ReadOnly: true),
        ],
        Secrets = [new SecretMount { Name = SecretName, Content = key, FileName = SecretFile }],
        Env =
        [
            new EnvVar("Stats__HostUrl", options.Value.HostUrl ?? $"http://{host.Value.ServiceName}:{host.Value.ContainerPort}"),
            new EnvVar("Stats__DiskProbePath", DiskProbePath),

            // swarm templates this at launch
            new EnvVar("Stats__NodeId", "{{.Node.ID}}"),
        ],
        ReadOnlyRootFilesystem = true,
        DropAllCapabilities = true,
        MemoryLimitMb = 128,
        CpuLimit = 0.25,
        MemoryReserveMb = 32,
    };

    /// null without a tag, swarm's digest comes after it
    public static string? TagOf(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        var withoutDigest = image.Split('@', 2)[0];

        // a colon before the last slash is a registry port
        var lastSegment = withoutDigest[(withoutDigest.LastIndexOf('/') + 1)..];
        var colon = lastSegment.IndexOf(':');
        return colon < 0 ? null : lastSegment[(colon + 1)..];
    }
}
