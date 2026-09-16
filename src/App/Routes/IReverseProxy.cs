namespace FifthBox.ServerManager.App.Routes;

public interface IReverseProxy
{
    string Render(IReadOnlyList<RouteConfig> routes, IReadOnlyList<HostCertificate> certificates);
}
