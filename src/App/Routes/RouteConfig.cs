using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.App.Routes;

/// A fully-resolved route ready for config generation: the public host/path and the upstream it proxies
/// to (the workload's swarm service name + port on the overlay). The App resolves workload ids to names
/// before handing these to the reverse proxy.
/// A hostname the edge holds a usable certificate for, with the paths the files are mounted at. Per
/// hostname rather than per route: certificates cover a hostname, routes are hostname + path, and two
/// routes on one host must not be able to disagree about it.
public record HostCertificate
{
    public required string Hostname { get; init; }
    public required string CertificatePath { get; init; }
    public required string PrivateKeyPath { get; init; }
}

public record RouteConfig
{
    public required string Hostname { get; init; }
    public required string Path { get; init; }
    public required string UpstreamService { get; init; }
    public required int UpstreamPort { get; init; }
    public UpstreamScheme Scheme { get; init; } = UpstreamScheme.Http;

    /// Emit the connection-upgrade headers so WebSocket connections proxy through.
    public bool WebSockets { get; init; }

    /// When set, the location gets `auth_basic` guarding it, reading this htpasswd file (delivered to
    /// nginx as its own config-object by the apply step).
    public string? AuthFilePath { get; init; }
}
