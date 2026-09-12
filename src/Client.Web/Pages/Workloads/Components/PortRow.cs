using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// A mutable port row for the workload form; converted to/from PortMapping by the model. A container
/// maps a host port onto a different container port; a native process binds one port directly, so its
/// rows leave Target unset and the model fills it in.
public sealed class PortRow
{
    public int Published { get; set; }
    public int Target { get; set; }
    public PortProtocol Protocol { get; set; } = PortProtocol.Both;
}
