namespace FifthBox.ServerManager.App.Routes;

/// Port for the reverse proxy (nginx). Render turns resolved routes into a config with no side effects
/// (used for preview and, in M4b, as the payload the apply step writes + reloads). Applying to a live
/// nginx service lands in M4b.
public interface IReverseProxy
{
    string Render(IReadOnlyList<RouteConfig> routes, IReadOnlyList<HostCertificate> certificates);
}
