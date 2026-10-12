using FifthBox.ServerManager.Shared.Cleanup;

namespace FifthBox.ServerManager.App.Cleanup;

public interface IImageCleanupService
{
    /// what Remove would take, nothing changes
    Task<ImageCleanupResponse> PreviewAsync(CancellationToken ct = default);

    Task<ImageCleanupResult> RemoveUnusedAsync(CancellationToken ct = default);
}

public sealed class ImageCleanupService(IDockerImages docker) : IImageCleanupService
{
    public async Task<ImageCleanupResponse> PreviewAsync(CancellationToken ct = default)
    {
        var images = await docker.ListImagesAsync(ct);
        var removable = ImageRetention.SelectForRemoval(images, await docker.ImageIdsInUseAsync(ct));
        return new ImageCleanupResponse
        {
            ImageCount = images.Count,
            ImageBytes = images.Sum(i => i.Size),
            VolumeCount = await docker.CountVolumesAsync(ct),
            Removable = [.. removable.OrderByDescending(i => i.Size).Select(i => new RemovableImageResponse
            {
                Id = i.Id, Name = ImageRetention.NameOf(i), Size = i.Size, Created = i.Created,
            })],
            ReclaimableBytes = removable.Sum(i => i.Size),
        };
    }

    public async Task<ImageCleanupResult> RemoveUnusedAsync(CancellationToken ct = default)
    {
        // worked out again here, not trusted from an old preview, something may have started using one since
        var removable = ImageRetention.SelectForRemoval(await docker.ListImagesAsync(ct), await docker.ImageIdsInUseAsync(ct));
        int removed = 0, skipped = 0;
        long freed = 0;
        foreach (var image in removable)
        {
            if (await docker.TryRemoveImageAsync(image.Id, ct))
            {
                removed++;
                freed += image.Size;
            }
            else
            {
                skipped++;
            }
        }
        return new ImageCleanupResult { Removed = removed, FreedBytes = freed, Skipped = skipped };
    }
}
