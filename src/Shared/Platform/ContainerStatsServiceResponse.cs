using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Platform;

public record ContainerStatsServiceResponse
{
    public bool Deployed { get; init; }

    /// false on a generated encryption key, there's nothing stable to sign with
    public bool CanDeploy { get; init; }

    public WorkloadState State { get; init; }
    public int RunningReplicas { get; init; }
    public int DesiredReplicas { get; init; }
    public string? Version { get; init; }
    public string? HostVersion { get; init; }

    /// deploy:direct leaves it behind
    public bool BehindHost { get; init; }
}
