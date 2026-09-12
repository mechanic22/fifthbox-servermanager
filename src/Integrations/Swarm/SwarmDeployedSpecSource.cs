using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Thin adapter: reads a live service back and hands it to SwarmSpecReader. Test-exempt; the reading is.
public sealed class SwarmDeployedSpecSource(IDockerClient client) : IDeployedSpecSource
{
    public async Task<DeployedSpec?> GetAsync(string serviceName, CancellationToken ct = default)
    {
        var services = await client.Swarm.ListServicesAsync(new ServicesListParameters
        {
            Filters = new ServiceFilter { Name = [serviceName] },
        }, ct);

        var service = services.FirstOrDefault(s => string.Equals(s.Spec?.Name, serviceName, StringComparison.Ordinal));
        if (service is null)
        {
            return null;
        }

        var tasks = await client.Tasks.ListAsync(new TasksListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["service"] = new Dictionary<string, bool> { [service.ID] = true },
            },
        }, ct);

        return SwarmSpecReader.Read(service, tasks);
    }
}
