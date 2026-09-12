using Docker.DotNet;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Workloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Integrations.Swarm;

public static class SwarmServiceCollectionExtensions
{
    /// Wires the Docker Swarm integration: one shared DockerClient for the given Engine API endpoint,
    /// plus the App ports it satisfies (node inventory, cluster lifecycle). The endpoint comes from the
    /// composition root (Host reads config) — the component doesn't reach into configuration itself.
    public static IServiceCollection AddSwarm(this IServiceCollection services, string dockerEndpoint)
    {
        services.AddSingleton<IDockerClient>(_ =>
            new DockerClientConfiguration(new Uri(dockerEndpoint)).CreateClient());
        services.AddSingleton<ISwarmEvents, SwarmEventSource>();
        services.AddSingleton<IWorkloadLogStream, SwarmLogStream>();
        services.AddSingleton<IWorkloadStatusSource, SwarmStatusSource>();
        services.AddScoped<INodeSource, SwarmNodeSource>();
        services.AddScoped<INodeControl, SwarmNodeControl>();
        services.AddScoped<IDeployedSpecSource, SwarmDeployedSpecSource>();
        services.TryAddScoped<ISwarmLifecycle, SwarmLifecycle>();
        services.AddScoped<IWorkloadBackend, SwarmBackend>();
        services.AddScoped<IPlatformServices, SwarmPlatformServices>();
        return services;
    }
}
