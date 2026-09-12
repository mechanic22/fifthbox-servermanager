using System.Net;

namespace FifthBox.ServerManager.App.Certificates;

/// Names no public CA will ever issue for — reserved and special-use suffixes (RFC 6761, RFC 8375,
/// ICANN's .internal), single-label names, and IP literals. Catching them here turns a wasted ACME
/// order and a Failed row into an answer the operator gets straight away.
public static class IssuableHostname
{
    public static readonly string[] ReservedSuffixes =
        ["local", "localhost", "test", "invalid", "example", "internal", "onion", "arpa"];

    /// Null when a public CA could issue for this hostname; otherwise why it can't.
    public static string? BlockedReason(string hostname, IEnumerable<string>? reservedSuffixes = null)
    {
        var host = (hostname ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0)
        {
            return "A valid hostname is required.";
        }

        // Before the dot check — an IPv6 literal has no dots either, and "not an IP address" is the
        // more useful answer.
        if (IPAddress.TryParse(host, out _))
        {
            return "Certificates are issued for hostnames, not IP addresses.";
        }

        if (!host.Contains('.'))
        {
            return $"'{host}' has no domain — certificates are only issued for names under a public "
                + "suffix, like app.example.com.";
        }

        foreach (var reserved in reservedSuffixes ?? ReservedSuffixes)
        {
            var suffix = reserved.Trim().TrimStart('.').ToLowerInvariant();
            if (suffix.Length > 0 && host.EndsWith($".{suffix}", StringComparison.Ordinal))
            {
                return $"'.{suffix}' is a reserved suffix that doesn't exist in public DNS, so no "
                    + "public CA can validate it.";
            }
        }

        return null;
    }
}
