using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FifthBox.ServerManager.Eventing;

/// Raises events to their handlers. Components publish through this and never touch a broker. The
/// implementation is in-process today; a broker can replace it without changing publishers.
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

/// Resolves every IEventHandler&lt;TEvent&gt; in a fresh scope and invokes them in turn. A handler that
/// throws is logged and skipped — one bad reaction never breaks the publish or the other handlers.
public sealed class InProcessEventPublisher(IServiceScopeFactory scopeFactory, ILogger<InProcessEventPublisher> logger)
    : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(@event, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event handler {Handler} failed for {Event}", handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
