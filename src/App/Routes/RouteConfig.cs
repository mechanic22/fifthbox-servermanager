using FifthBox.ServerManager.Shared.Routes;

namespace FifthBox.ServerManager.App.Routes;

/// per hostname not per route, so two routes on one host can't disagree
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

    public bool WebSockets { get; init; }

    public int? MaxBodySizeMb { get; init; }

    /// when set, the location gets auth_basic against this htpasswd file
    public string? AuthFilePath { get; init; }
}
