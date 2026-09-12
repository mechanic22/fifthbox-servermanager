using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Cluster;

/// What the backend currently believes about the cluster. Written by whatever observes a change — the
/// docker event stream, an agent's push, the reconcile — and read by everything else, so a page load
/// costs a dictionary lookup instead of a fan-out to the daemon.
public interface IClusterState
{
    /// False until the first write. An empty store and an empty cluster look identical otherwise, and
    /// answering "no nodes" before we've ever looked is a lie.
    bool Hydrated { get; }

    IReadOnlyList<NodeResponse> Nodes { get; }

    WorkloadRuntimeStatus? StatusFor(string workloadId);

    /// Every status observed so far, keyed by workload id — one read for a page listing many workloads.
    IReadOnlyDictionary<string, WorkloadRuntimeStatus> Statuses { get; }

    /// Each setter answers "did this change anything" so the caller knows whether to tell clients.
    bool SetNodes(IReadOnlyList<NodeResponse> nodes);

    bool SetWorkloadStatus(string workloadId, WorkloadRuntimeStatus status);

    void ForgetWorkload(string workloadId);
}

public sealed class ClusterState : IClusterState
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, WorkloadRuntimeStatus> _statuses = new(StringComparer.Ordinal);
    private IReadOnlyList<NodeResponse> _nodes = [];

    public bool Hydrated { get; private set; }

    public IReadOnlyList<NodeResponse> Nodes
    {
        get
        {
            lock (_gate)
            {
                return _nodes;
            }
        }
    }

    public WorkloadRuntimeStatus? StatusFor(string workloadId) => _statuses.GetValueOrDefault(workloadId);

    public IReadOnlyDictionary<string, WorkloadRuntimeStatus> Statuses => _statuses.ToDictionary();

    public bool SetNodes(IReadOnlyList<NodeResponse> nodes)
    {
        lock (_gate)
        {
            // Hydrated flips even when nothing differs: a cluster that genuinely has no nodes still
            // counts as having been looked at.
            var changed = !Hydrated || NodeSnapshot.Differs(_nodes, nodes);
            _nodes = nodes;
            Hydrated = true;
            return changed;
        }
    }

    public bool SetWorkloadStatus(string workloadId, WorkloadRuntimeStatus status)
    {
        var changed = true;
        _statuses.AddOrUpdate(
            workloadId,
            status,
            (_, existing) =>
            {
                changed = !Same(existing, status);
                return status;
            });

        return changed;
    }

    private static readonly IReadOnlyList<WorkloadTask> NoTasks = [];

    /// The record's own equality compares Tasks by reference, so two identical readings would always
    /// look different and every sweep would broadcast. Compare the rest by value and the list by sequence.
    private static bool Same(WorkloadRuntimeStatus a, WorkloadRuntimeStatus b)
        => a with { Tasks = NoTasks } == b with { Tasks = NoTasks } && a.Tasks.SequenceEqual(b.Tasks);

    public void ForgetWorkload(string workloadId) => _statuses.TryRemove(workloadId, out _);
}
