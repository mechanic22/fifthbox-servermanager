using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Platform;

/// our own infra services, nginx etc
public record PlatformServiceResponse
{
    public required string Name { get; init; }
    public string? Image { get; init; }
    public WorkloadState State { get; init; }
    public int RunningReplicas { get; init; }
    public int DesiredReplicas { get; init; }
}
