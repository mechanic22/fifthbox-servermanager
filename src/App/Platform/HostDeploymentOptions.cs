namespace FifthBox.ServerManager.App.Platform;

public sealed class HostDeploymentOptions
{
    public string ServiceName { get; set; } = "fbsm-host";
    public string Image { get; set; } = "docker.5thbox.com/fbsm/servermanager:latest";
    public string DataVolume { get; set; } = "fbsm-data";
    public string DataPath { get; set; } = "/data";
    public int PublishedPort { get; set; } = 5080;
    public int ContainerPort { get; set; } = 8080;

    /// must match Cluster:OverlayNetwork so nginx can resolve the Host
    public string OverlayNetwork { get; set; } = "fbsm-overlay";

    /// the documented docker run container, removed on handover so two Hosts never share the socket and db
    public string UnmanagedContainerName { get; set; } = "fbsm-host";
}
