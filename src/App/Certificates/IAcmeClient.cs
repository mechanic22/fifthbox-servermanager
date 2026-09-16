namespace FifthBox.ServerManager.App.Certificates;

public interface IAcmeClient
{
    /// staging certs are untrusted, the UI needs to say so
    bool UsesProductionCa { get; }

    Task<IssuedCertificate> IssueAsync(IReadOnlyList<string> hostnames, CancellationToken ct = default);
}

/// usually dns not pointing here yet or port 80 unreachable, expected so record it, don't crash
public sealed class CertificateIssuanceException(string message, Exception? inner = null) : Exception(message, inner);

/// internal only, chain and key never cross the API
public sealed record IssuedCertificate
{
    public required string PemChain { get; init; }
    public required string PrivateKeyPem { get; init; }
    public required DateTimeOffset NotBefore { get; init; }
    public required DateTimeOffset NotAfter { get; init; }
}
