using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

public interface IContainerStatsNotifier
{
    Task NotifyAsync(ContainerStatsReport report, CancellationToken ct = default);
}
