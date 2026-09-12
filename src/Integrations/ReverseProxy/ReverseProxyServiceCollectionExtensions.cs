using FifthBox.ServerManager.App.Routes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Integrations.ReverseProxy;

public static class ReverseProxyServiceCollectionExtensions
{
    /// Wires the nginx reverse-proxy integration (the App's IReverseProxy port).
    public static IServiceCollection AddReverseProxy(this IServiceCollection services)
    {
        services.TryAddScoped<IReverseProxy, NginxReverseProxy>();
        return services;
    }
}
