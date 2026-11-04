using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Realtime;

public sealed class ContainerStatsNotifier(IEventPublisher events) : IContainerStatsNotifier
{
    public Task NotifyAsync(ContainerStatsReport report, CancellationToken ct = default) =>
        events.PublishAsync(new ContainerStatsReceived(report), ct);
}
