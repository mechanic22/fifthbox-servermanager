namespace FifthBox.ServerManager.Shared.Workloads;

public enum VolumeMountType
{
    /// Source is the volume name
    Volume,

    /// Source is an absolute path on the node
    Bind,
}

public record VolumeMount(VolumeMountType Type, string Source, string Target, bool ReadOnly);
