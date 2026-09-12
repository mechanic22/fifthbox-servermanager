using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadGroupResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }

    /// How many workloads the caller can see in here — not how many are in it.
    public int WorkloadCount { get; init; }

    /// What the caller may do with this group. None means it's only here to hold the tree together:
    /// an ancestor of something they can reach, with nothing granted on it.
    public AccessLevel Access { get; init; }
}
