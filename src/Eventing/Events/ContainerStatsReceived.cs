using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Eventing.Events;

public sealed record ContainerStatsReceived(ContainerStatsReport Report);
