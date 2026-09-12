namespace FifthBox.ServerManager.Shared.Workloads;

public record CreateWorkloadGroupRequest
{
    public string Name { get; init; } = string.Empty;

    /// Parent group id, or null for a top-level group.
    public string? ParentId { get; init; }
}
