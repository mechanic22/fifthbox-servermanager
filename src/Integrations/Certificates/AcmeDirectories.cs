using Certes.Acme;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class AcmeDirectories
{
    /// Anything that isn't a recognised name has to be an absolute URL — a typo like "prod" must not
    /// silently fall back to the production CA and burn the duplicate-certificate allowance.
    public static Uri Resolve(string? directory) => (directory ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" or "staging" => WellKnownServers.LetsEncryptStagingV2,
        "production" or "live" => WellKnownServers.LetsEncryptV2,
        var other when Uri.TryCreate(other, UriKind.Absolute, out var uri) => uri,
        var other => throw new ArgumentException(
            $"Acme:Directory must be 'staging', 'production', or a directory URL — got '{other}'.", nameof(directory)),
    };

    public static bool IsProduction(string? directory) => Resolve(directory) == WellKnownServers.LetsEncryptV2;
}
