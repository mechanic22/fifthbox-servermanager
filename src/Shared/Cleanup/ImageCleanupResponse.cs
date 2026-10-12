namespace FifthBox.ServerManager.Shared.Cleanup;

public record ImageCleanupResponse
{
    public int ImageCount { get; init; }
    public long ImageBytes { get; init; }
    public int VolumeCount { get; init; }
    public List<RemovableImageResponse> Removable { get; init; } = [];

    /// an upper bound, images that share layers free less than their sizes add up to
    public long ReclaimableBytes { get; init; }
}

public record RemovableImageResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public DateTimeOffset Created { get; init; }
}

public record ImageCleanupResult
{
    public int Removed { get; init; }
    public long FreedBytes { get; init; }

    /// docker refused these, usually a stopped container still points at them
    public int Skipped { get; init; }
}
