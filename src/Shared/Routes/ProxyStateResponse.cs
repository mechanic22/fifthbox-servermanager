using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Routes;

/// The nginx edge as it is, next to the config it should be running. Saved route and certificate
/// changes don't reach traffic until the edge is redeployed, so the client needs both halves.
public sealed class ProxyStateResponse
{
    public required WorkloadRuntimeStatus Status { get; init; }

    /// Routes or certificates have changed since the edge was last deployed.
    public bool PendingChanges { get; init; }

    /// Null when the edge has never been deployed from here.
    public DateTimeOffset? LastAppliedAt { get; init; }
}
