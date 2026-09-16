using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadGroupResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }

    /// what the caller can see, not the real total
    public int WorkloadCount { get; init; }

    /// None = only here to hold the tree together
    public AccessLevel Access { get; init; }
}
