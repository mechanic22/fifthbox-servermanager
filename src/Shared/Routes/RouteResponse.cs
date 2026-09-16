namespace FifthBox.ServerManager.Shared.Routes;

public record RouteResponse
{
    public required string Id { get; init; }
    public required string Hostname { get; init; }
    public required string Path { get; init; }
    public RouteTarget Target { get; init; }
    public string? WorkloadId { get; init; }
    public string? UpstreamHost { get; init; }
    public UpstreamScheme UpstreamScheme { get; init; }
    public string? WorkloadName { get; init; }
    public int TargetPort { get; init; }
    public bool Enabled { get; init; } = true;

    public bool WebSockets { get; init; }
    public int? MaxBodySizeMb { get; init; }
    public bool BasicAuthEnabled { get; init; }
    public string? BasicAuthUsername { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
