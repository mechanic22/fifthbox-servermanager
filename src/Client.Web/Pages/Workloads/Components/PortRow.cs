using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// native rows leave Target unset, the model fills it in
public sealed class PortRow
{
    public int Published { get; set; }
    public int Target { get; set; }
    public PortProtocol Protocol { get; set; } = PortProtocol.Both;
}
