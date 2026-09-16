using FifthBox.ServerManager.App.Workloads;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Integrations.Agents;

public static class AgentsServiceCollectionExtensions
{
    /// the Host has to register the IAgentCommandChannel
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<IWorkloadBackend, AgentBackend>();
        return services;
    }
}
