using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Eventing.Events;

/// A workload's observed runtime state changed. Raised by the agent relay when an agent reports a
/// supervision transition; carries the workload id so a client can match it to what it's showing.
public sealed record WorkloadStatusChanged(string WorkloadId, WorkloadRuntimeStatus Status);
