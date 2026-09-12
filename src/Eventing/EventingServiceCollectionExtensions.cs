using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FifthBox.ServerManager.Eventing;

public static class EventingServiceCollectionExtensions
{
    /// Wires Eventing. In-process publisher today; event handlers are registered by the components
    /// that own the reaction (e.g. the Host registers the SignalR broadcaster).
    public static IServiceCollection AddEventing(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventPublisher, InProcessEventPublisher>();
        return services;
    }
}
