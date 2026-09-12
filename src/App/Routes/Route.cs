using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.App.Routes;

/// Maps a public hostname (+ path) to a workload's container port. nginx turns these into server
/// blocks that proxy over the overlay network to the workload's service name. Persisted desired state.
public class Route
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Hostname { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public RouteTarget Target { get; set; }

    /// Set when Target is Workload — the container workload this proxies to.
    public string? WorkloadId { get; set; }

    /// Set when Target is External — a host or IP reachable from the nginx container. Never a URL:
    /// it is interpolated straight into proxy_pass.
    public string? UpstreamHost { get; set; }

    public UpstreamScheme UpstreamScheme { get; set; } = UpstreamScheme.Http;
    public int TargetPort { get; set; }

    /// Off keeps the route defined but out of the generated nginx config — the way to pull a site
    /// offline without deleting how to bring it back.
    public bool Enabled { get; set; } = true;

    /// nginx site options (the old "HTTP" tab).
    public bool WebSockets { get; set; }
    public bool BasicAuthEnabled { get; set; }
    public string? BasicAuthUsername { get; set; }

    /// htpasswd-format bcrypt hash ($2y$…). Never plaintext, never returned to clients.
    public string? BasicAuthPasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
