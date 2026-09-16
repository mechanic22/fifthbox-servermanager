using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Cluster;

/// written by whatever sees a change, read by everything else so a page load skips the daemon
public interface IClusterState
{
    /// false until the first write, so an empty store doesn't lie as "no nodes"
    bool Hydrated { get; }

    IReadOnlyList<NodeResponse> Nodes { get; }

    WorkloadRuntimeStatus? StatusFor(string workloadId);

    IReadOnlyDictionary<string, WorkloadRuntimeStatus> Statuses { get; }

    /// setters return whether anything changed, so callers know to tell clients
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
            // flips even when nothing differs, an empty cluster still got looked at
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

    /// record equality compares Tasks by reference, so every sweep would broadcast without this
    private static bool Same(WorkloadRuntimeStatus a, WorkloadRuntimeStatus b)
        => a with { Tasks = NoTasks } == b with { Tasks = NoTasks } && a.Tasks.SequenceEqual(b.Tasks);

    public void ForgetWorkload(string workloadId) => _statuses.TryRemove(workloadId, out _);
}
