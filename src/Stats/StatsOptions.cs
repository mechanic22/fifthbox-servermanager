namespace FifthBox.ServerManager.Stats;

public sealed class StatsOptions
{
    /// the Host by service name on the overlay, not through nginx
    public string HostUrl { get; set; } = "http://fbsm-host:8080";

    /// a swarm secret, tmpfs only
    public string KeyFile { get; set; } = "/run/secrets/fbsm-stats-key";

    public string DockerSocket { get; set; } = "/var/run/docker.sock";
    public int IntervalSeconds { get; set; } = 10;

    /// swarm fills this in with {{.Node.ID}}
    public string? NodeId { get; set; }

    public string DiskProbePath { get; set; } = "/var/fbsm-disk-probe";
}
