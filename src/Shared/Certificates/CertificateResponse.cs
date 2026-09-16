namespace FifthBox.ServerManager.Shared.Certificates;

public enum CertificateStatus
{
    /// mid-request or waiting for the next renewal tick
    Pending,
    Valid,
    Failed,
}

/// metadata only, keeps the private key off the contract so nothing leaks it by accident
public record CertificateResponse
{
    public required string Hostname { get; init; }
    public CertificateStatus Status { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public DateTimeOffset? NotAfter { get; init; }

    /// the CA's words, null once one succeeds
    public string? LastError { get; init; }
}

public record EnableHttpsRequest
{
    public string Hostname { get; init; } = string.Empty;
}
