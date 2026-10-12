using System.Net;
using Docker.DotNet;
using FifthBox.ServerManager.App.Cleanup;

namespace FifthBox.ServerManager.Integrations.Swarm;

public sealed class SwarmVolumes(IDockerClient client) : IDockerVolumes
{
    public async Task<VolumeRemoval> RemoveAsync(string name, CancellationToken ct = default)
    {
        try
        {
            await client.Volumes.RemoveAsync(name, force: false, ct);
            return VolumeRemoval.Removed;
        }
        catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return VolumeRemoval.InUse;
        }
        catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return VolumeRemoval.Missing;
        }
    }
}
