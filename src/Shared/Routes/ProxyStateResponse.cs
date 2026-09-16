using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Routes;

/// route and cert changes don't hit traffic until the edge redeploys, so the client needs both
public sealed class ProxyStateResponse
{
    public required WorkloadRuntimeStatus Status { get; init; }

    /// routes or certs changed since the last edge deploy
    public bool PendingChanges { get; init; }

    /// null if the edge was never deployed from here
    public DateTimeOffset? LastAppliedAt { get; init; }
}
