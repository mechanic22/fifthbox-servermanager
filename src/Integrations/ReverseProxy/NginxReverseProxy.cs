using FifthBox.ServerManager.App.Routes;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Integrations.ReverseProxy;

public sealed class NginxReverseProxy(IOptions<ReverseProxyOptions> options) : IReverseProxy
{
    public string Render(IReadOnlyList<RouteConfig> routes, IReadOnlyList<HostCertificate> certificates, IReadOnlySet<string> wwwRedirects) =>
        NginxConfigGenerator.Generate(routes, options.Value.AcmeUpstream, certificates, options.Value.HttpsPort, wwwRedirects, options.Value.HttpPort);
}
