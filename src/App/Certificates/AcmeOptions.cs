namespace FifthBox.ServerManager.App.Certificates;

/// Bound from the "Acme" config section.
public sealed class AcmeOptions
{
    /// "staging" or "production" (or a directory URL for another CA). Staging by default on purpose:
    /// production allows five duplicate certificates a week, and a first-run wiring bug can burn that
    /// allowance and lock the platform out for days. Going live is an explicit config change.
    public string Directory { get; set; } = "staging";

    /// How long to keep polling one authorization before giving up.
    public int ValidationTimeoutSeconds { get; set; } = 60;

    /// Renew once a certificate has this many days left. Thirty gives ~30 daily attempts before it
    /// matters, so a missed tick or a transient CA failure costs one chance out of thirty.
    public int RenewBeforeDays { get; set; } = 30;

    /// Hostname suffixes to refuse before an order is placed. Configured values are *added* to the
    /// built-in reserved set, so a site-specific one like "lan" is a one-line appsettings change.
    public List<string> ReservedSuffixes { get; set; } = [.. IssuableHostname.ReservedSuffixes];
}
