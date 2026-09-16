using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Eventing.Events;

public sealed record WorkloadStatusChanged(string WorkloadId, WorkloadRuntimeStatus Status);
