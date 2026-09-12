using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Eventing;
using FifthBox.ServerManager.Eventing.Events;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Host.Realtime;

public interface IClusterStateWriter
{
    Task RefreshNodesAsync(CancellationToken ct = default);

    /// Re-read one workload's runtime status, given the docker service name an event named.
    Task RefreshWorkloadAsync(string serviceName, CancellationToken ct = default);

    /// Re-read every workload's status in one sweep: containers from docker, and native workloads only
    /// far enough to notice their agent is gone.
    Task RefreshWorkloadsAsync(CancellationToken ct = default);
}

/// The single place a change in cluster state turns into an event for connected clients. Everything that
/// observes the cluster — the docker event stream, the reconcile — comes through here rather than
/// publishing for itself, so the "only when it changed" rule is written once.
public sealed class ClusterStateWriter(
    IServiceScopeFactory scopeFactory,
    IClusterState state,
    IAgentRegistry agents,
    IEventPublisher publisher,
    ILogger<ClusterStateWriter> logger) : IClusterStateWriter
{
    private string? _lastFailure;

    public async Task RefreshWorkloadAsync(string serviceName, CancellationToken ct = default)
    {
        if (!SwarmNaming.TryWorkloadName(serviceName, out var name))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workload = await scope.ServiceProvider.GetRequiredService<IWorkloadRepository>()
                .FindByNameAsync(name, ct);

            // A service we don't have a record for: someone else's, or one we just deleted.
            if (workload is null)
            {
                return;
            }

            var workloads = scope.ServiceProvider.GetRequiredService<IWorkloadService>();
            var status = await workloads.GetStatusAsync(Caller.System, workload.Id, ct);

            // Or, not just the status change: a rollout settling is often invisible in the status — the
            // replicas were already running — and a client sitting on the workload page has no other way
            // to learn its deploy finished.
            var settled = await workloads.SettleRevisionAsync(workload.Id, status, ct);

            if (state.SetWorkloadStatus(workload.Id, status) || settled)
            {
                await publisher.PublishAsync(new WorkloadStatusChanged(workload.Id, status), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not refresh status for service {Service}", serviceName);
        }
    }

    public async Task RefreshWorkloadsAsync(CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var deployed = await scope.ServiceProvider.GetRequiredService<IWorkloadStatusSource>().GetAllAsync(ct);
            var workloads = await scope.ServiceProvider.GetRequiredService<IWorkloadRepository>().ListAsync(ct);
            var service = scope.ServiceProvider.GetRequiredService<IWorkloadService>();

            // Nothing else ever revisits a native workload — the agent pushes its own changes, so an
            // agent that dies leaves its last push standing as if it were still true.
            foreach (var workload in workloads.Where(w => w.Kind == WorkloadKind.Native))
            {
                if (workload.AgentId is not null && agents.IsOnline(workload.AgentId))
                {
                    continue; // still connected, and its own reports are fresher than anything here
                }

                var offline = new WorkloadRuntimeStatus
                {
                    Name = workload.Name,
                    Deployed = false,
                    State = WorkloadState.NotDeployed,
                    Detail = "the agent is offline",
                };

                if (state.SetWorkloadStatus(workload.Id, offline))
                {
                    await publisher.PublishAsync(new WorkloadStatusChanged(workload.Id, offline), ct);
                }
            }

            foreach (var workload in workloads.Where(w => w.Kind == WorkloadKind.Container))
            {
                var serviceName = SwarmNaming.ServiceName(workload.Name);

                // Absent from the sweep means the service isn't there — stopped, or never deployed.
                var status = deployed.GetValueOrDefault(serviceName) ?? new WorkloadRuntimeStatus
                {
                    Name = serviceName,
                    Deployed = false,
                    State = WorkloadState.NotDeployed,
                };

                var settled = await service.SettleRevisionAsync(workload.Id, status, ct);

                if (state.SetWorkloadStatus(workload.Id, status) || settled)
                {
                    await publisher.PublishAsync(new WorkloadStatusChanged(workload.Id, status), ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Workload status sweep failed; retrying next tick");
        }
    }

    public async Task RefreshNodesAsync(CancellationToken ct = default)
    {
        try
        {
            // INodeService is scoped (its sources reach the database); this writer is not.
            await using var scope = scopeFactory.CreateAsyncScope();

            if (await scope.ServiceProvider.GetRequiredService<INodeService>().RefreshAsync(ct))
            {
                await publisher.PublishAsync(new NodeStateChanged(state.Nodes), ct);
            }

            _lastFailure = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // An unbootstrapped host fails this every single time, forever, so only the first of a
            // repeating failure gets a stack trace — otherwise it buries the real errors.
            if (_lastFailure == ex.Message)
            {
                logger.LogDebug("Node refresh still failing: {Reason}", ex.Message);
            }
            else
            {
                _lastFailure = ex.Message;
                logger.LogError(ex, "Node refresh failed");
            }
        }
    }
}
