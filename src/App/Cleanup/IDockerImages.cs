namespace FifthBox.ServerManager.App.Cleanup;

public record LocalImage(string Id, IReadOnlyList<string> Tags, IReadOnlyList<string> Digests, long Size, DateTimeOffset Created);

/// the images and volumes on the node this host's docker socket belongs to
public interface IDockerImages
{
    Task<IReadOnlyList<LocalImage>> ListImagesAsync(CancellationToken ct = default);

    /// image ids any container uses, running or stopped
    Task<IReadOnlySet<string>> ImageIdsInUseAsync(CancellationToken ct = default);

    Task<int> CountVolumesAsync(CancellationToken ct = default);

    /// false when docker refuses because something still uses it
    Task<bool> TryRemoveImageAsync(string id, CancellationToken ct = default);
}
