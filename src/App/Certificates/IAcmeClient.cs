namespace FifthBox.ServerManager.App.Certificates;

/// Orders certificates from an ACME CA. Implemented in the Certificates integration; the HTTP-01
/// challenges it has to answer go out through <see cref="IAcmeChallengeStore"/>.
public interface IAcmeClient
{
    /// Whether the configured directory is the production CA. Staging issues untrusted certificates, so
    /// the UI has to be able to say so.
    bool UsesProductionCa { get; }

    Task<IssuedCertificate> IssueAsync(IReadOnlyList<string> hostnames, CancellationToken ct = default);
}

/// Issuance failed at the CA — usually the hostname not resolving to this edge yet, or the challenge
/// not being reachable over port 80. Expected often enough that callers record it rather than crash.
public sealed class CertificateIssuanceException(string message, Exception? inner = null) : Exception(message, inner);

/// A freshly issued certificate. Neither the chain nor the key ever crosses the API — this is internal.
public sealed record IssuedCertificate
{
    public required string PemChain { get; init; }
    public required string PrivateKeyPem { get; init; }
    public required DateTimeOffset NotBefore { get; init; }
    public required DateTimeOffset NotAfter { get; init; }
}
