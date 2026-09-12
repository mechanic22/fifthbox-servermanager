namespace FifthBox.ServerManager.Shared.Platform;

public record BackupFileResponse
{
    public required string Name { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
