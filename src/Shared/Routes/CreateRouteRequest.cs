namespace FifthBox.ServerManager.Shared.Routes;

public record CreateRouteRequest
{
    public string Hostname { get; init; } = string.Empty;
    public string Path { get; init; } = "/";
    public RouteTarget Target { get; init; }
    public string? WorkloadId { get; init; }
    public string? UpstreamHost { get; init; }
    public UpstreamScheme UpstreamScheme { get; init; }
    public int TargetPort { get; init; }

    public bool WebSockets { get; init; }
    public bool BasicAuthEnabled { get; init; }
    public string? BasicAuthUsername { get; init; }

    /// Plaintext, write-only — hashed server-side, never stored or returned.
    public string? BasicAuthPassword { get; init; }
}
