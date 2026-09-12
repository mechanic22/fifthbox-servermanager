using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Platform;

/// One of ServerManager's own infrastructure services (nginx, etc.) with its observed status.
public record PlatformServiceResponse
{
    public required string Name { get; init; }
    public string? Image { get; init; }
    public WorkloadState State { get; init; }
    public int RunningReplicas { get; init; }
    public int DesiredReplicas { get; init; }
}
