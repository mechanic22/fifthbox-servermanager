using FifthBox.ServerManager.App.Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificatesServiceCollectionExtensions
{
    public static IServiceCollection AddCertificates(this IServiceCollection services)
    {
        services.TryAddScoped<IAcmeClient, CertesAcmeClient>();
        return services;
    }
}
