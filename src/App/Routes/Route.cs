using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.App.Routes;

public class Route
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public RouteTarget Target { get; set; }

    /// only when Target is Workload
    public string? WorkloadId { get; set; }

    /// only when Target is External. host or ip, never a url, it goes straight into proxy_pass
    public string? UpstreamHost { get; set; }

    public UpstreamScheme UpstreamScheme { get; set; } = UpstreamScheme.Http;
    public int TargetPort { get; set; }

    /// off keeps the route but drops it from the nginx config
    public bool Enabled { get; set; } = true;

    public bool WebSockets { get; set; }

    /// null keeps nginx's 1 MB default, 0 lifts the cap and streams the body
    public int? MaxBodySizeMb { get; set; }

    public bool BasicAuthEnabled { get; set; }
    public string? BasicAuthUsername { get; set; }

    /// bcrypt htpasswd hash, never plaintext, never returned
    public string? BasicAuthPasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
