using FifthBox.ServerManager.Shared.Certificates;

namespace FifthBox.ServerManager.App.Certificates;

/// One hostname's TLS certificate. The row's existence *is* the opt-in to HTTPS: no row means the
/// hostname stays on plain port 80. Certificates are per hostname while routes are per hostname + path,
/// which is why this isn't a flag on Route.
public class Certificate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Hostname { get; set; } = string.Empty;

    public CertificateStatus Status { get; set; } = CertificateStatus.Pending;

    /// PEM chain, leaf first. Public information — it's what every client is shown.
    public string? PemChain { get; set; }

    /// The private key, encrypted with ISecretProtector. Never leaves the App layer in the clear.
    public string? PrivateKeyEnc { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? NotAfter { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
