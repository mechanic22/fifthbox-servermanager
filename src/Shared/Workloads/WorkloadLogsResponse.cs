namespace FifthBox.ServerManager.Shared.Workloads;

public enum WorkloadLogRuns
{
    Current,
    Previous,
}

public record WorkloadLogsResponse
{
    public IReadOnlyList<WorkloadLogLine> Lines { get; init; } = [];

    /// at least one run couldn't be read in time, what did come back is still in Lines
    public bool Incomplete { get; init; }
}
