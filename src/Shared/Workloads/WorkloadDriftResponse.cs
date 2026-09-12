namespace FifthBox.ServerManager.Shared.Workloads;

/// Whether the running service still matches the config it was deployed from.
public record WorkloadDriftResponse
{
    /// Which parts differ — "image", "replicas", "environment", "ports". Empty means no drift, which is
    /// also the answer when there's nothing deployed to compare against.
    public IReadOnlyList<string> Fields { get; init; } = [];

    public bool HasDrift => Fields.Count > 0;
}
