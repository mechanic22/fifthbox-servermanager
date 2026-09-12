using FifthBox.ServerManager.Shared.Platform;

namespace FifthBox.ServerManager.App.Platform;

/// Lists and bounces ServerManager's own infrastructure services (labelled fbsm.role=platform — nginx
/// today, letsencrypt later). Implemented in the Swarm integration as a read model over the swarm;
/// distinct from IWorkloadBackend, which runs user workloads.
public interface IPlatformServices
{
    Task<IReadOnlyList<PlatformServiceResponse>> ListAsync(CancellationToken ct = default);
    Task RestartAsync(string name, CancellationToken ct = default);

    /// Whether a swarm service by this name exists — used to tell a managed Host install from an
    /// unmanaged one, regardless of which process is asking.
    Task<bool> ServiceExistsAsync(string name, CancellationToken ct = default);

    /// Node id of the engine we're talking to. Pins the Host service to the node holding its volume.
    Task<string?> LocalNodeIdAsync(CancellationToken ct = default);
}
