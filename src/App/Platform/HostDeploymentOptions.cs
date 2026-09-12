namespace FifthBox.ServerManager.App.Platform;

/// Shape of the managed install, bound from the "HostDeployment" config section. Defaults are the
/// documented install, so nothing needs configuring unless you deviate from it.
public sealed class HostDeploymentOptions
{
    public string ServiceName { get; set; } = "fbsm-host";
    public string Image { get; set; } = "docker.5thbox.com/fbsm/servermanager:latest";
    public string DataVolume { get; set; } = "fbsm-data";
    public string DataPath { get; set; } = "/data";
    public int PublishedPort { get; set; } = 5080;
    public int ContainerPort { get; set; } = 8080;

    /// Matches Cluster:OverlayNetwork — the managed install joins it so nginx can resolve the Host.
    public string OverlayNetwork { get; set; } = "fbsm-overlay";

    /// Name of the plain container started by the documented `docker run` — removed as part of the
    /// handover so two Hosts never share the socket and database.
    public string UnmanagedContainerName { get; set; } = "fbsm-host";
}
