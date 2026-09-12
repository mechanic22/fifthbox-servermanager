namespace FifthBox.ServerManager.Shared.Certificates;

/// Every hostname the edge serves, whether or not it has a certificate — the uncertified ones are what
/// you'd enable HTTPS for, so they belong in the same list.
public record TlsOverviewResponse
{
    /// True while pointed at Let's Encrypt staging. Certificates issued there are real but signed by an
    /// untrusted root, so browsers reject them — worth saying out loud rather than looking like a bug.
    public bool StagingCa { get; init; }

    public IReadOnlyList<TlsHostResponse> Hosts { get; init; } = [];
}

public record TlsHostResponse
{
    public required string Hostname { get; init; }

    /// How many routes this hostname serves. Disabling HTTPS affects all of them, which is worth
    /// showing before someone clicks it.
    public int RouteCount { get; init; }

    /// Null when the hostname has no certificate at all — plain HTTP.
    public CertificateStatus? Status { get; init; }

    public DateTimeOffset? NotAfter { get; init; }
    public string? LastError { get; init; }

    /// Null when a public CA could issue for this hostname; otherwise why it can't — reserved suffixes
    /// like .local, single-label names, IP literals. Enabling HTTPS isn't offered for these.
    public string? IssuanceBlockedReason { get; init; }
}
