namespace FifthBox.ServerManager.Eventing;

/// Handles a published event in-process. Register one per (event, reaction); the publisher invokes
/// every handler registered for the event type. Implementations live in the component that owns the
/// reaction — e.g. the Host's SignalR bridge for realtime pushes.
public interface IEventHandler<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
