using FifthBox.ServerManager.App.Routes;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Integrations.ReverseProxy;

/// nginx implementation of the reverse-proxy port. For now it only renders config (delegating to the
/// tested generator); deploying nginx and writing + reloading the config lands in M4b.
public sealed class NginxReverseProxy(IOptions<ReverseProxyOptions> options) : IReverseProxy
{
    public string Render(IReadOnlyList<RouteConfig> routes, IReadOnlyList<HostCertificate> certificates) =>
        NginxConfigGenerator.Generate(routes, options.Value.AcmeUpstream, certificates, options.Value.HttpsPort);
}
