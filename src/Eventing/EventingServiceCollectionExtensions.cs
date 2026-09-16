using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Eventing;

public static class EventingServiceCollectionExtensions
{
    /// handlers are registered by whoever owns the reaction, not here
    public static IServiceCollection AddEventing(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventPublisher, InProcessEventPublisher>();
        return services;
    }
}
