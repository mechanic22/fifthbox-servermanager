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
