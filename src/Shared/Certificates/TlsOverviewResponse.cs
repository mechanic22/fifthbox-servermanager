namespace FifthBox.ServerManager.Shared.Certificates;

/// every hostname the edge serves, uncertified ones too since that's what you'd enable https on
public record TlsOverviewResponse
{
    /// staging certs have an untrusted root, browsers reject them
    public bool StagingCa { get; init; }

    public IReadOnlyList<TlsHostResponse> Hosts { get; init; } = [];
}

public record TlsHostResponse
{
    public required string Hostname { get; init; }

    /// disabling https hits all of these, so show it first
    public int RouteCount { get; init; }

    /// null = no cert, plain http
    public CertificateStatus? Status { get; init; }

    public DateTimeOffset? NotAfter { get; init; }
    public string? LastError { get; init; }

    /// why a public CA can't issue (.local, single label, ip), null if it can
    public string? IssuanceBlockedReason { get; init; }
}
