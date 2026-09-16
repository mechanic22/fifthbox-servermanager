using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Certificates;

/// row existing is the https opt-in, per hostname not per route so it's not a flag on Route
public class Certificate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Hostname { get; set; } = string.Empty;

    public CertificateStatus Status { get; set; } = CertificateStatus.Pending;

    public string? PemChain { get; set; }

    /// encrypted with ISecretProtector, never leaves App in the clear
    public string? PrivateKeyEnc { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? NotAfter { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
