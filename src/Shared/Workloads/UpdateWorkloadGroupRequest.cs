namespace FifthBox.ServerManager.Shared.Workloads;

public record UpdateWorkloadGroupRequest
{
    public string Name { get; init; } = string.Empty;
}
