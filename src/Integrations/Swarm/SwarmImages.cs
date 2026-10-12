using System.Net;
using Docker.DotNet;
using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Cleanup;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmImages(IDockerClient client) : IDockerImages
{
    public async Task<IReadOnlyList<LocalImage>> ListImagesAsync(CancellationToken ct = default) =>
        [.. (await client.Images.ListImagesAsync(new ImagesListParameters { All = false }, ct))
            .Select(i => new LocalImage(i.ID, [.. i.RepoTags ?? []], [.. i.RepoDigests ?? []], i.Size, new DateTimeOffset(i.Created, TimeSpan.Zero)))];

    public async Task<IReadOnlySet<string>> ImageIdsInUseAsync(CancellationToken ct = default) =>
        (await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, ct))
            .Select(c => c.ImageID).ToHashSet();

    public async Task<int> CountVolumesAsync(CancellationToken ct = default) =>
        (await client.Volumes.ListAsync(ct)).Volumes?.Count ?? 0;

    public async Task<bool> TryRemoveImageAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await client.Images.DeleteImageAsync(id, new ImageDeleteParameters { Force = false }, ct);
            return true;
        }
        catch (DockerApiException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
