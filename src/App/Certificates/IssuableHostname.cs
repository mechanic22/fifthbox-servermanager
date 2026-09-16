using System.Net;

namespace FifthBox.ServerManager.App.Certificates;

/// names no public CA will issue (reserved suffixes, single labels, IPs), caught before wasting an order
public static class IssuableHostname
{
    public static readonly string[] ReservedSuffixes =
        ["local", "localhost", "test", "invalid", "example", "internal", "onion", "arpa"];

    /// null when issuable, otherwise why not
    public static string? BlockedReason(string hostname, IEnumerable<string>? reservedSuffixes = null)
    {
        var host = (hostname ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0)
        {
            return "A valid hostname is required.";
        }

        // before the dot check, ipv6 has no dots either and "IP address" is the better answer
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
