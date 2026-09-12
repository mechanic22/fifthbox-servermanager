namespace FifthBox.ServerManager.App.Cluster;

public sealed class ClusterOptions
{
    /// The overlay network ServerManager ensures exists for hosted workloads to share.
    public string OverlayNetwork { get; set; } = "fbsm-overlay";
}
