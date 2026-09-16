namespace FifthBox.ServerManager.App.Routes;

public static class DefaultHostname
{
    /// null without a root domain, stable because workload names never change
    public static string? For(string workloadName, string? rootDomain)
    {
        var domain = (rootDomain ?? string.Empty).Trim().Trim('.').ToLowerInvariant();
        var name = (workloadName ?? string.Empty).Trim().ToLowerInvariant();

        return domain.Length == 0 || name.Length == 0 ? null : $"{name}.{domain}";
    }
}
