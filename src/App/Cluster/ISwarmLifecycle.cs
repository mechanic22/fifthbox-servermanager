namespace FifthBox.ServerManager.App.Cluster;

/// Port for cluster lifecycle ops on the swarm backend. The implementation in /Integrations/Swarm is a
/// thin adapter — each method is one Docker call. The idempotent orchestration (init-or-adopt, ensure
/// network) lives above it in ClusterService, so it's testable without Docker.
public interface ISwarmLifecycle
{
    Task<SwarmState> GetStateAsync(CancellationToken ct = default);
    Task<SwarmJoinTokens> GetJoinTokensAsync(CancellationToken ct = default);
    Task InitSwarmAsync(CancellationToken ct = default);
    Task<bool> NetworkExistsAsync(string name, CancellationToken ct = default);
    Task CreateOverlayNetworkAsync(string name, CancellationToken ct = default);
}
