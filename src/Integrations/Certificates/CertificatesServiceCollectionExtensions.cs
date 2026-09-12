using FifthBox.ServerManager.App.Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Integrations.Certificates;

public static class CertificatesServiceCollectionExtensions
{
    /// Wires the Let's Encrypt integration (the App's IAcmeClient port).
    public static IServiceCollection AddCertificates(this IServiceCollection services)
    {
        services.TryAddScoped<IAcmeClient, CertesAcmeClient>();
        return services;
    }
}
