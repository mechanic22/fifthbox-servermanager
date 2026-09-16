namespace FifthBox.ServerManager.App.Platform;

public class PlatformSettings
{
    public string Id { get; set; } = "current";
    public string? RootDomain { get; set; }
    public string? AcmeEmail { get; set; }

    /// "manage" gives manage.example.com, blank means no hostname for the manager
    public string? ManagerPrefix { get; set; } = "manage";

    /// encrypted, only leaves Storage via IAcmeAccountStore, keep it off PlatformSettingsResponse
    public string? AcmeAccountKeyEnc { get; set; }
    public string? AcmeAccountDirectory { get; set; }

    /// hash of config, htpasswd files and certs last deployed to the edge, spots unapplied changes
    public string? AppliedProxyHash { get; set; }
    public DateTimeOffset? ProxyAppliedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
