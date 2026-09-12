using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// A mutable volume/bind-mount row for the workload form; converted to/from VolumeMount by the form.
public sealed class VolumeRow
{
    public VolumeMountType Type { get; set; } = VolumeMountType.Volume;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}
