namespace FifthBox.ServerManager.Shared.Workloads;

public record WorkloadDriftResponse
{
    /// "image", "replicas", "environment", "ports". also empty when nothing's deployed
    public IReadOnlyList<string> Fields { get; init; } = [];

    public bool HasDrift => Fields.Count > 0;
}
