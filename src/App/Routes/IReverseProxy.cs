namespace FifthBox.ServerManager.App.Routes;

public interface IReverseProxy
{
    /// wwwRedirects are bare hostnames whose www. twin 301s to them
    string Render(IReadOnlyList<RouteConfig> routes, IReadOnlyList<HostCertificate> certificates, IReadOnlySet<string> wwwRedirects);
}
