using FifthBox.ServerManager.Shared.Platform;

namespace FifthBox.ServerManager.App.Platform;

/// ServerManager's own infra services (fbsm.role=platform), not user workloads
public interface IPlatformServices
{
    Task<IReadOnlyList<PlatformServiceResponse>> ListAsync(CancellationToken ct = default);
    Task RestartAsync(string name, CancellationToken ct = default);

    /// tells a managed Host install from an unmanaged one
    Task<bool> ServiceExistsAsync(string name, CancellationToken ct = default);

    /// node id of the engine we talk to, used to pin the Host to its volume's node
    Task<string?> LocalNodeIdAsync(CancellationToken ct = default);
}
