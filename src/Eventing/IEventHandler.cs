namespace FifthBox.ServerManager.Eventing;

/// register one per event + reaction, every one registered gets called
public interface IEventHandler<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
