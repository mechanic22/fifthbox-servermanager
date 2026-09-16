using FifthBox.ServerManager.App.Routes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Integrations.ReverseProxy;

public static class ReverseProxyServiceCollectionExtensions
{
    public static IServiceCollection AddReverseProxy(this IServiceCollection services)
    {
        services.TryAddScoped<IReverseProxy, NginxReverseProxy>();
        return services;
    }
}
