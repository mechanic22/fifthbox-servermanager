namespace FifthBox.ServerManager.Shared.Workloads;

/// One entry in a managed workload's directory. Paths are always relative to the install root — the
/// agent's real filesystem layout never leaves the agent.
public record WorkloadFileEntry
{
    public required string Name { get; init; }

    /// Relative to the install root, using '/' whatever the agent runs on.
    public required string Path { get; init; }

    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
}

public record WorkloadFileContent
{
    public required string Path { get; init; }
    public required string Text { get; init; }
}

public record WriteWorkloadFileRequest
{
    public string Path { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}
