namespace FifthBox.ServerManager.Shared.Workloads;

public record CreateWorkloadGroupRequest
{
    public string Name { get; init; } = string.Empty;

    public string? ParentId { get; init; }
}
