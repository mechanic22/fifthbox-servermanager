using FifthBox.ServerManager.App.Workloads;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Integrations.Agents;

public static class AgentsServiceCollectionExtensions
{
    /// Wires the agent workload backend. The command channel (transport over the AgentHub) is supplied
    /// by the Host composition root.
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<IWorkloadBackend, AgentBackend>();
        return services;
    }
}
