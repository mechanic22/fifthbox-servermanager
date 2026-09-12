namespace FifthBox.ServerManager.Shared.Certificates;

public enum CertificateStatus
{
    /// Asked for, not yet issued — either mid-request or waiting for the next renewal tick.
    Pending,
    Valid,
    Failed,
}

/// Metadata only. The chain is public information but the private key is not, and keeping both off the
/// contract means no endpoint can leak one by accident.
public record CertificateResponse
{
    public required string Hostname { get; init; }
    public CertificateStatus Status { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public DateTimeOffset? NotAfter { get; init; }

    /// Why the last attempt failed, in the CA's words. Null once one succeeds.
    public string? LastError { get; init; }
}

public record EnableHttpsRequest
{
    public string Hostname { get; init; } = string.Empty;
}
