namespace FifthBox.ServerManager.App.Platform;

/// Platform-wide settings (single row). The root domain hosted apps get subdomains under, and the ACME
/// registration email. Both are staged for M5/TLS (letsencrypt).
public class PlatformSettings
{
    public string Id { get; set; } = "current";
    public string? RootDomain { get; set; }
    public string? AcmeEmail { get; set; }

    /// Subdomain the manager itself answers on, under RootDomain — "manage" gives
    /// manage.example.com. Blank means the manager gets no hostname of its own.
    public string? ManagerPrefix { get; set; } = "manage";

    /// The platform's ACME account, encrypted. Never leaves Storage except through IAcmeAccountStore —
    /// PlatformSettingsResponse deliberately doesn't carry it.
    public string? AcmeAccountKeyEnc { get; set; }
    public string? AcmeAccountDirectory { get; set; }

    /// Fingerprint of the payload the nginx edge was last deployed with — config, htpasswd files and
    /// certificate material. Compared against the current desired payload to spot unapplied changes.
    public string? AppliedProxyHash { get; set; }
    public DateTimeOffset? ProxyAppliedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
