namespace FifthBox.ServerManager.App.Routes;

public static class DefaultHostname
{
    /// The address a workload gets under the platform's root domain, or null when there's no root
    /// domain configured. Workload names are already slugs and immutable, so this is stable for the
    /// life of the workload.
    public static string? For(string workloadName, string? rootDomain)
    {
        var domain = (rootDomain ?? string.Empty).Trim().Trim('.').ToLowerInvariant();
        var name = (workloadName ?? string.Empty).Trim().ToLowerInvariant();

        return domain.Length == 0 || name.Length == 0 ? null : $"{name}.{domain}";
    }
}
