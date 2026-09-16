using FifthBox.ServerManager.Shared.Cluster;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.Cluster;

public interface IClusterService
{
    Task<ClusterStatusResponse> GetStatusAsync(CancellationToken ct = default);
    Task<ClusterJoinResponse> GetJoinInfoAsync(CancellationToken ct = default);
    Task<ClusterStatusResponse> BootstrapAsync(CancellationToken ct = default);
}

public sealed class ClusterService(ISwarmLifecycle lifecycle, IOptions<ClusterOptions> options) : IClusterService
{
    private readonly string _network = options.Value.OverlayNetwork;

    public async Task<ClusterStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var state = await lifecycle.GetStateAsync(ct);
        var networkExists = state.IsInSwarm && await lifecycle.NetworkExistsAsync(_network, ct);
        return ToStatus(state, networkExists);
    }

    public async Task<ClusterJoinResponse> GetJoinInfoAsync(CancellationToken ct = default)
    {
        var state = await lifecycle.GetStateAsync(ct);
        if (!state.IsInSwarm || !state.IsManager)
        {
            return new ClusterJoinResponse { Available = false };
        }

        var tokens = await lifecycle.GetJoinTokensAsync(ct);
        var address = $"{tokens.ManagerAddress}:2377";
        return new ClusterJoinResponse
        {
            Available = true,
            ManagerAddress = address,
            WorkerJoinCommand = $"docker swarm join --token {tokens.Worker} {address}",
            ManagerJoinCommand = $"docker swarm join --token {tokens.Manager} {address}",
        };
    }

    /// explicit opt-in, never at startup. inits or adopts the swarm, ensures the overlay, safe to rerun
    public async Task<ClusterStatusResponse> BootstrapAsync(CancellationToken ct = default)
    {
        var state = await lifecycle.GetStateAsync(ct);
        if (!state.IsInSwarm)
        {
            await lifecycle.InitSwarmAsync(ct);
        }

        if (!await lifecycle.NetworkExistsAsync(_network, ct))
        {
            await lifecycle.CreateOverlayNetworkAsync(_network, ct);
        }

        return await GetStatusAsync(ct);
    }

    private ClusterStatusResponse ToStatus(SwarmState s, bool networkExists) => new()
    {
        Membership = s.Membership,
        IsInSwarm = s.IsInSwarm,
        IsManager = s.IsManager,
        NodeId = s.NodeId,
        NodeAddress = s.NodeAddress,
        NodeCount = s.NodeCount,
        ManagerCount = s.ManagerCount,
        Error = s.Error,
        ManagedNetworkName = _network,
        ManagedNetworkExists = networkExists,
    };
}
