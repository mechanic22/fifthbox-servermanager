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

    /// takes the docker service name, not the workload name
    Task RefreshWorkloadAsync(string serviceName, CancellationToken ct = default);

    /// natives only get checked for a dead agent
    Task RefreshWorkloadsAsync(CancellationToken ct = default);
}

/// everything watching the cluster publishes through here so "only when it changed" lives in one place
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

            // not ours, or we just deleted it
            if (workload is null)
            {
                return;
            }

            var workloads = scope.ServiceProvider.GetRequiredService<IWorkloadService>();
            var status = await workloads.GetStatusAsync(Caller.System, workload.Id, ct);

            // a settled rollout often doesn't change status, but the page still needs to hear it finished
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

            // agents push their own changes, so a dead agent's last push would stand forever
            foreach (var workload in workloads.Where(w => w.Kind == WorkloadKind.Native))
            {
                if (workload.AgentId is not null && agents.IsOnline(workload.AgentId))
                {
                    continue; // still connected, its own reports are fresher
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

                // missing from the sweep means stopped or never deployed
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
            // INodeService is scoped, this writer isn't
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
            // an unbootstrapped host fails this forever, only the first one gets a stack trace
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
