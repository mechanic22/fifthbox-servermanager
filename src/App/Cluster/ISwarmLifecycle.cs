namespace FifthBox.ServerManager.App.Cluster;

public interface ISwarmLifecycle
{
    Task<SwarmState> GetStateAsync(CancellationToken ct = default);
    Task<SwarmJoinTokens> GetJoinTokensAsync(CancellationToken ct = default);
    Task InitSwarmAsync(CancellationToken ct = default);
    Task<bool> NetworkExistsAsync(string name, CancellationToken ct = default);
    Task CreateOverlayNetworkAsync(string name, CancellationToken ct = default);
}
