namespace FifthBox.ServerManager.Shared.Workloads;

public enum VolumeMountType
{
    /// Docker-managed named volume — Source is the volume name.
    Volume,

    /// Host path bind mount — Source is an absolute path on the node.
    Bind,
}

/// A mount into the container. Volume → Source is a named volume; Bind → Source is a host path. Target is
/// the path inside the container.
public record VolumeMount(VolumeMountType Type, string Source, string Target, bool ReadOnly);
