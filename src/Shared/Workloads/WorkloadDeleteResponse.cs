namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadDeleteResponse
{
    public List<string> RemovedVolumes { get; init; } = [];

    /// asked for but still in use or not on the host's node, remove by hand with docker volume rm
    public List<string> KeptVolumes { get; init; } = [];
}
