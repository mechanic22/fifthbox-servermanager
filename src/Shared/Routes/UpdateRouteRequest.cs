namespace FifthBox.ServerManager.Shared.Routes;

public record UpdateRouteRequest
{
    public string Hostname { get; init; } = string.Empty;
    public string Path { get; init; } = "/";
    public RouteTarget Target { get; init; }
    public string? WorkloadId { get; init; }
    public string? UpstreamHost { get; init; }
    public UpstreamScheme UpstreamScheme { get; init; }
    public int TargetPort { get; init; }

    public bool WebSockets { get; init; }

    /// Null = nginx's 1 MB default; 0 = unlimited.
    public int? MaxBodySizeMb { get; init; }

    public bool BasicAuthEnabled { get; init; }
    public string? BasicAuthUsername { get; init; }

    /// Plaintext, write-only. Blank on update = keep the existing password.
    public string? BasicAuthPassword { get; init; }
}
