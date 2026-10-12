using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Cleanup;

public enum VolumeRemoval
{
    Removed,
    InUse,
    Missing,
}

/// named volumes on the node this host's docker socket belongs to
public interface IDockerVolumes
{
    Task<VolumeRemoval> RemoveAsync(string name, CancellationToken ct = default);
}

public sealed class WorkloadVolumeCleanup(IDockerVolumes volumes)
{
    /// the service's containers are still stopping right after it's removed, docker holds the volume till they're gone
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public int Attempts { get; init; } = 15;

    /// the docker names of a container workload's named volumes, binds aren't ours to delete
    public static IReadOnlyList<string> VolumeNames(WorkloadDeployment deployment, WorkloadKind kind) =>
        kind != WorkloadKind.Container ? []
        : [.. deployment.Mounts.Where(m => m.Type == VolumeMountType.Volume).Select(m => SwarmNaming.VolumeName(deployment.Name, m.Source)).Distinct()];

    /// the names it couldn't remove, still in use after the wait or not on this node
    public async Task<IReadOnlyList<string>> RemoveAsync(IReadOnlyList<string> names, CancellationToken ct = default)
    {
        var left = new List<string>();
        foreach (var name in names)
        {
            var outcome = await volumes.RemoveAsync(name, ct);
            for (var attempt = 1; outcome == VolumeRemoval.InUse && attempt < Attempts; attempt++)
            {
                await Task.Delay(RetryDelay, ct);
                outcome = await volumes.RemoveAsync(name, ct);
            }
            if (outcome != VolumeRemoval.Removed)
                left.Add(name);
        }
        return left;
    }
}
