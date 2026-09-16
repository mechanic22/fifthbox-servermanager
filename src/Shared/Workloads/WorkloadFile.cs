namespace FifthBox.ServerManager.Shared.Workloads;

/// paths are relative to the install root, the agent's real layout never leaves the agent
public record WorkloadFileEntry
{
    public required string Name { get; init; }

    /// always '/' whatever the agent runs on
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
