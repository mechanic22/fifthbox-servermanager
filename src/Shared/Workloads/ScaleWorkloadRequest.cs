namespace FifthBox.ServerManager.Shared.Workloads;

public record ScaleWorkloadRequest
{
    public int Replicas { get; init; }
}
