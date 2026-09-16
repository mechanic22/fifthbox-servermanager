using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Cluster;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmLifecycle(IDockerClient client) : ISwarmLifecycle
{
    public async Task<SwarmState> GetStateAsync(CancellationToken ct = default)
    {
        var info = await client.System.GetSystemInfoAsync(ct);
        return SwarmStateMapper.ToState(info.Swarm);
    }

    public async Task<SwarmJoinTokens> GetJoinTokensAsync(CancellationToken ct = default)
    {
        var info = await client.System.GetSystemInfoAsync(ct);
        var inspect = await client.Swarm.InspectSwarmAsync(ct);
        return new SwarmJoinTokens(inspect.JoinTokens.Worker, inspect.JoinTokens.Manager, info.Swarm.NodeAddr);
    }

    public Task InitSwarmAsync(CancellationToken ct = default) =>
        client.Swarm.InitSwarmAsync(new SwarmInitParameters { ListenAddr = "0.0.0.0" }, ct);

    public async Task<bool> NetworkExistsAsync(string name, CancellationToken ct = default)
    {
        var networks = await client.Networks.ListNetworksAsync(cancellationToken: ct);
        return networks.Any(n => string.Equals(n.Name, name, StringComparison.Ordinal));
    }

    public Task CreateOverlayNetworkAsync(string name, CancellationToken ct = default) =>
        client.Networks.CreateNetworkAsync(new NetworksCreateParameters
        {
            Name = name,
            Driver = "overlay",
            Scope = "swarm",
            Attachable = true,
        }, ct);
}
