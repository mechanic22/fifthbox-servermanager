using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmStatusSource(IDockerClient client) : IWorkloadStatusSource
{
    public async Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetAllAsync(CancellationToken ct = default)
    {
        // unfiltered on purpose, the manager answers for every node out of raft
        var services = await client.Swarm.ListServicesAsync(cancellationToken: ct);
        var tasks = await client.Tasks.ListAsync(new TasksListParameters(), ct);
        return SwarmStatusMapper.Derive(services, tasks);
    }
}
