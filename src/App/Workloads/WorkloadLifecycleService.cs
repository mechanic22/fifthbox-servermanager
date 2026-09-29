using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public sealed class WorkloadLifecycleService(
    WorkloadLoader loader,
    IWorkloadRepository repository,
    IWorkloadAccess access,
    IWorkloadBackendResolver backends,
    IDeployedSpecSource deployedSpecs,
    IClusterState state,
    WorkloadDeploymentFactory deployments,
    TimeProvider clock)
{
    public async Task<WorkloadRuntimeStatus> DeployAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);

        var deployment = deployments.ToDeployment(workload);
        var backend = backends.Resolve(workload.Kind);
        await backend.DeployAsync(deployment, ct);

        workload.DesiredState = WorkloadDesiredState.Running;
        var firstDeploy = workload.Revisions.Count == 0;
        var recorded = WorkloadRevisions.RecordRevisionIfChanged(workload, clock);
        var status = await backend.GetStatusAsync(deployment, ct);

        // settle a first deploy now, an update's status may still be the old rollout about to roll back
        // native always, nothing else ever settles one
        if (recorded && (firstDeploy || workload.Kind == WorkloadKind.Native))
        {
            WorkloadRevisions.Settle(workload, status);
        }

        // always saved, redeploying unchanged config is still how you undo a Stop
        await repository.UpdateAsync(workload, ct);

        return status;
    }
    public async Task<WorkloadRuntimeStatus> ScaleAsync(Caller caller, string id, int replicas, CancellationToken ct = default)
    {
        WorkloadValidation.ValidateReplicas(replicas);
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);

        if (workload.Mode == WorkloadMode.Global)
        {
            throw new ConflictException(
                $"'{workload.Name}' runs one copy on every node. Add or drain nodes to change how many there are.");
        }

        if (WorkloadValidation.SingleInstance(workload.Placement, workload.Mounts) && replicas != 1)
        {
            throw new ValidationException(nameof(ScaleWorkloadRequest.Replicas),
                workload.Placement == WorkloadPlacement.Node
                    ? "This workload runs on a chosen node, so it runs a single instance."
                    : "This workload has a volume, which lives on one node, so it runs a single instance.");
        }

        workload.Replicas = replicas;
        // keep the running revision in step so a scale isn't a pending change
        WorkloadRevisions.SyncRunningReplicas(workload, replicas);
        workload.UpdatedAt = clock.GetUtcNow();
        await repository.UpdateAsync(workload, ct);

        var deployment = deployments.ToDeployment(workload);
        var backend = backends.Resolve(workload.Kind);
        await backend.ScaleAsync(deployment, replicas, ct);
        return await backend.GetStatusAsync(deployment, ct);
    }
    public async Task<bool> SettleRevisionAsync(string workloadId, WorkloadRuntimeStatus status, CancellationToken ct = default)
    {
        var workload = await repository.FindByIdAsync(workloadId, ct);
        if (workload is null)
        {
            return false;
        }

        // both, settling and learning placement are independent
        var settled = WorkloadRevisions.Settle(workload, status);
        var placed = WorkloadRevisions.RememberPlacement(workload, status);
        if (!settled && !placed)
        {
            return false;
        }

        await repository.UpdateAsync(workload, ct);
        return true;
    }
    public async Task<WorkloadRuntimeStatus> RestartAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);
        var running = WorkloadRevisions.Running(workload)
            ?? throw new ConflictException("Deploy the workload before restarting it.");

        // bouncing at zero replicas recreates nothing, then it shows up as Missing
        if (workload.DesiredState == WorkloadDesiredState.Stopped)
        {
            throw new ConflictException($"'{workload.Name}' is stopped. Start it rather than restarting it.");
        }

        // running config, not the edited one
        var deployment = deployments.ToDeployment(workload, running);
        var backend = backends.Resolve(workload.Kind);

        // undeploy keeps revisions, so having one doesn't mean there's a service to bounce
        if (!(await backend.GetStatusAsync(deployment, ct)).Deployed)
        {
            throw new ConflictException($"'{workload.Name}' is not deployed. Deploy it before restarting it.");
        }

        await backend.RestartAsync(deployment, ct);
        return await backend.GetStatusAsync(deployment, ct);
    }
    public async Task<WorkloadRuntimeStatus> UpdateAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);

        if (workload.Source.Kind == SourceKind.None)
        {
            throw new ConflictException($"'{workload.Name}' has no source to update from.");
        }

        // desired config not a revision, a just-fixed source shouldn't need a deploy first
        var deployment = deployments.ToDeployment(workload);
        var backend = backends.Resolve(workload.Kind);

        await backend.UpdateAsync(deployment, ct);
        return await backend.GetStatusAsync(deployment, ct);
    }
    public async Task SendConsoleAsync(Caller caller, string id, string text, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException(nameof(SendConsoleRequest.Text), "Enter a command to send.");
        }

        var running = WorkloadRevisions.Running(workload)
            ?? throw new ConflictException("Deploy the workload before sending it commands.");

        await backends.Resolve(workload.Kind).SendConsoleAsync(deployments.ToDeployment(workload, running), text.Trim(), ct);
    }
    public async Task<WorkloadRuntimeStatus> StartAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);
        var running = WorkloadRevisions.Running(workload)
            ?? throw new ConflictException("Deploy the workload before starting it.");

        // running revision, so starting back up doesn't ship undeployed edits
        var deployment = deployments.ToDeployment(workload, running);
        var backend = backends.Resolve(workload.Kind);
        await backend.StartAsync(deployment, ct);
        await SetDesiredStateAsync(workload, WorkloadDesiredState.Running, ct);
        return await backend.GetStatusAsync(deployment, ct);
    }
    public async Task StopAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Operate, ct);
        await backends.Resolve(workload.Kind).StopAsync(deployments.ToDeployment(workload), ct);
        await SetDesiredStateAsync(workload, WorkloadDesiredState.Stopped, ct);
    }
    public async Task UndeployAsync(Caller caller, string id, CancellationToken ct = default)
    {
        WorkloadLoader.RequireAdmin(caller);
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);
        await backends.Resolve(workload.Kind).UndeployAsync(deployments.ToDeployment(workload), ct);
        await SetDesiredStateAsync(workload, WorkloadDesiredState.Stopped, ct);
    }
    public async Task<WorkloadDriftResponse> GetDriftAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);

        // stopped sits at zero by design and agents have no spec, neither is drift
        if (workload.Kind != WorkloadKind.Container || workload.DesiredState == WorkloadDesiredState.Stopped)
        {
            return new WorkloadDriftResponse();
        }

        if (WorkloadRevisions.Running(workload) is not { } running)
        {
            return new WorkloadDriftResponse();
        }

        // unsettled revision means mid-rollout, comparing now reports our own deploy as someone else's
        if (WorkloadRevisions.Newest(workload) is { Applied: false })
        {
            return new WorkloadDriftResponse();
        }

        // ToDeployment decrypts secrets but the revision holds ciphertext, comparing that directly always drifts
        var expected = deployments.ToDeployment(workload, running);
        var live = await deployedSpecs.GetAsync(expected.Name, ct);

        return live is null
            ? new WorkloadDriftResponse()
            : new WorkloadDriftResponse { Fields = SpecDrift.Compare(expected, live) };
    }
    private async Task SetDesiredStateAsync(Workload w, WorkloadDesiredState desired, CancellationToken ct)
    {
        if (w.DesiredState == desired)
        {
            return;
        }

        w.DesiredState = desired;
        await repository.UpdateAsync(w, ct);
    }
    public async Task<WorkloadRuntimeStatus> GetStatusAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);
        return await backends.Resolve(workload.Kind).GetStatusAsync(deployments.ToDeployment(workload), ct);
    }
    public async Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetStatusesAsync(Caller caller, CancellationToken ct = default)
    {
        var map = await access.MapAsync(caller, ct);
        var observed = state.Statuses;
        var statuses = new Dictionary<string, WorkloadRuntimeStatus>(StringComparer.Ordinal);

        foreach (var workload in await repository.ListAsync(ct))
        {
            if (map.ForWorkload(workload.Id, workload.GroupId) < AccessLevel.View)
            {
                continue;
            }

            if (observed.TryGetValue(workload.Id, out var known))
            {
                statuses[workload.Id] = known;
                continue;
            }

            try
            {
                var fresh = await backends.Resolve(workload.Kind).GetStatusAsync(deployments.ToDeployment(workload), ct);
                state.SetWorkloadStatus(workload.Id, fresh);
                statuses[workload.Id] = fresh;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // one unreachable backend shouldn't blank every other status
            }
        }

        return statuses;
    }
    public async Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(Caller caller, string id, int tail, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);
        return await backends.Resolve(workload.Kind).GetLogsAsync(deployments.ToDeployment(workload), Math.Clamp(tail, 1, 2000), ct);
    }
}
