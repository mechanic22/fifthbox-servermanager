using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Routes;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public interface IWorkloadService
{
    Task<IReadOnlyList<WorkloadResponse>> ListAsync(Caller caller, CancellationToken ct = default);
    Task<WorkloadResponse> GetAsync(Caller caller, string id, CancellationToken ct = default);
    Task<WorkloadResponse> CreateAsync(Caller caller, CreateWorkloadRequest request, CancellationToken ct = default);
    Task<WorkloadResponse> UpdateAsync(Caller caller, string id, UpdateWorkloadRequest request, CancellationToken ct = default);
    Task DeleteAsync(Caller caller, string id, CancellationToken ct = default);

    Task<WorkloadRuntimeStatus> DeployAsync(Caller caller, string id, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> ScaleAsync(Caller caller, string id, int replicas, CancellationToken ct = default);

    /// Hands a native workload to another agent: stops it where it runs, then starts it on the target.
    /// The running revision crosses untouched, so a move applies no unsaved edits and leaves no pending
    /// change behind.
    /// Agents this workload could move to. Needs Configure — the same level as the move itself, since
    /// choosing a machine is meaningless without seeing which machines there are.
    Task<IReadOnlyList<AgentResponse>> MoveTargetsAsync(Caller caller, string id, CancellationToken ct = default);

    Task<MoveWorkloadResponse> MoveAsync(Caller caller, string id, string agentId, CancellationToken ct = default);

    /// Bounce the running instance on its current (last-deployed) config — no config change. Throws if it
    /// was never deployed.
    Task<WorkloadRuntimeStatus> RestartAsync(Caller caller, string id, CancellationToken ct = default);

    /// Send one line to the workload's console.
    Task SendConsoleAsync(Caller caller, string id, string text, CancellationToken ct = default);

    /// Fetch the workload's files. Returns as soon as the acquire has started — it can run for a long
    /// time, and progress arrives on the status and log channels.
    Task<WorkloadRuntimeStatus> UpdateAsync(Caller caller, string id, CancellationToken ct = default);

    /// Undo a Stop, on the config it was last running.
    Task<WorkloadRuntimeStatus> StartAsync(Caller caller, string id, CancellationToken ct = default);
    Task StopAsync(Caller caller, string id, CancellationToken ct = default);

    /// Take it off the backend altogether, keeping the definition and its history. Admin-only: unlike
    /// Stop it throws away the service and everything swarm was holding with it.
    Task UndeployAsync(Caller caller, string id, CancellationToken ct = default);

    /// Whether the running service still matches the revision it was deployed from — i.e. whether
    /// somebody changed it outside ServerManager.
    Task<WorkloadDriftResponse> GetDriftAsync(Caller caller, string id, CancellationToken ct = default);
    Task<WorkloadRuntimeStatus> GetStatusAsync(Caller caller, string id, CancellationToken ct = default);

    /// Status for every workload at once. Answers from observed state where it has it, and only pays the
    /// backend for the ones nobody has seen yet — a list page shouldn't cost one round trip per row.
    Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetStatusesAsync(Caller caller, CancellationToken ct = default);

    /// Recent output from whichever backend runs it.
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(Caller caller, string id, int tail, CancellationToken ct = default);

    /// Just this workload's routes. Managing routes is admin-only, but someone who can see a workload
    /// should be able to see the address it answers on without being handed every route in the system.
    Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(Caller caller, string id, CancellationToken ct = default);

    Task<IReadOnlyList<WorkloadRevisionResponse>> GetRevisionsAsync(Caller caller, string id, CancellationToken ct = default);

    /// Reconcile the revision history against what the backend turned out to do with the last deploy.
    /// Called from wherever a fresh status arrives; a no-op unless the rollout has settled.
    /// True when this changed something worth telling clients about.
    Task<bool> SettleRevisionAsync(string workloadId, WorkloadRuntimeStatus status, CancellationToken ct = default);

    /// Loads an older revision's config back into the saved (desired) config. Does not deploy — the
    /// caller redeploys to actually apply it.
    Task<WorkloadResponse> RevertAsync(Caller caller, string id, int revisionNumber, CancellationToken ct = default);
}

/// Manages workload definitions and drives their lifecycle. Workloads are polymorphic: Container
/// (swarm) or Native (agent). Container lifecycle runs through <see cref="IWorkloadBackend"/> (the
/// swarm today); native execution arrives with the agent backend (M6b).
public sealed class WorkloadService(
    IWorkloadRepository repository,
    WorkloadLoader loader,
    WorkloadLifecycleService lifecycle,
    IAgentRepository agents,
    IAgentRegistry agentConnections,
    IWorkloadGroupRepository groups,
    IWorkloadAccess access,
    IAccessGrantRepository accessGrants,
    IWorkloadBackendResolver backends,
    IRouteService routes,
    IPlatformSettingsRepository platformSettings,
    INodeService nodes,
    ISecretProtector protector,
    WorkloadDeploymentFactory deployments,
    TimeProvider clock) : IWorkloadService
{
    public async Task<IReadOnlyList<WorkloadResponse>> ListAsync(Caller caller, CancellationToken ct = default)
    {
        var map = await access.MapAsync(caller, ct);
        var workloads = await repository.ListAsync(ct);
        var agentNames = await AgentNamesAsync(ct);
        var groupNames = await GroupNamesAsync(ct);
        return workloads
            .Select(w => (Workload: w, Level: map.ForWorkload(w.Id, w.GroupId)))
            .Where(x => x.Level >= AccessLevel.View)
            .Select(x => Map(x.Workload,
                agentNames.GetValueOrDefault(x.Workload.AgentId ?? string.Empty),
                groupNames.GetValueOrDefault(x.Workload.GroupId ?? string.Empty),
                x.Level))
            .ToList();
    }

    // The single-workload read is what the config form loads, so it carries secrets in the clear — but
    // only for someone who can edit the config and could read them off the running instance anyway. The
    // list and revision history stay redacted so they don't ship every secret in the system on a visit.
    public async Task<WorkloadResponse> GetAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, level) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);
        var response = await MapAsync(workload, level, ct);
        return level >= AccessLevel.Configure ? response with { Env = deployments.Decrypted(workload.Env) } : response;
    }

    public async Task<WorkloadResponse> CreateAsync(Caller caller, CreateWorkloadRequest request, CancellationToken ct = default)
    {
        WorkloadLoader.RequireAdmin(caller);
        var name = WorkloadValidation.ValidateName(request.Name);
        if (await repository.NameExistsAsync(name, excludingId: null, ct))
        {
            throw new ConflictException($"A workload named '{name}' already exists.");
        }

        var now = clock.GetUtcNow();
        var workload = new Workload { Name = name, Target = request.Target, CreatedAt = now, UpdatedAt = now };
        workload.GroupId = await ResolveGroupAsync(request.GroupId, ct);

        if (request.Target == WorkloadTarget.Swarm)
        {
            workload.Kind = WorkloadKind.Container;
            workload.Image = WorkloadValidation.ValidateImage(request.Image);
            WorkloadValidation.ValidateReplicas(request.Replicas);
            WorkloadValidation.ValidatePorts(request.Ports);
            WorkloadValidation.ValidateResources(request.MemoryLimitMb, request.CpuLimit, request.MemoryReserveMb, request.CpuReserve);
            WorkloadValidation.ValidateMounts(request.Mounts);
            // Both of these mean one node, and one node means one instance: a named volume is on exactly
            // one machine, and a chosen node is a choice of one.
            workload.Replicas = WorkloadValidation.SingleInstance(request.Placement, request.Mounts) ? 1 : request.Replicas;
            workload.Placement = request.Placement;
            workload.NodeId = await ValidatePlacementAsync(request.Placement, request.Mode, request.NodeId, ct);
            workload.Ports = WorkloadValidation.PublishedPorts(request.Ports);
            workload.Mode = request.Mode;
            workload.MemoryLimitMb = request.MemoryLimitMb;
            workload.CpuLimit = request.CpuLimit;
            workload.MemoryReserveMb = request.MemoryReserveMb;
            workload.CpuReserve = request.CpuReserve;
            workload.Mounts = [.. request.Mounts];
            WorkloadValidation.ApplyHealth(workload, request.HealthCommand, request.HealthIntervalSeconds, request.HealthTimeoutSeconds, request.HealthRetries, request.HealthStartPeriodSeconds);
        }
        else
        {
            workload.Kind = WorkloadKind.Native;
            workload.AgentId = await ValidateAgentAsync(request.AgentId, ct);
            workload.Command = WorkloadValidation.ValidateCommand(request.Command);
            workload.Args = [.. request.Args];
            workload.WorkingDirectory = WorkloadValidation.Blank(request.WorkingDirectory);
            workload.RestartPolicy = request.RestartPolicy;
            workload.StopGraceSeconds = WorkloadValidation.ValidateStopGrace(request.StopGraceSeconds);
            workload.StopCommand = WorkloadValidation.Blank(request.StopCommand);
            workload.ManagedDirectory = request.ManagedDirectory;
            workload.Source = ValidateSource(request.Source, workload.Source, workload.ManagedDirectory);
            // Normalize before validating — a native row carries no target port, so it would fail the
            // 1..65535 check on a field the operator was never asked for.
            workload.Ports = WorkloadValidation.NativePorts(request.Ports);
            WorkloadValidation.ValidatePorts(workload.Ports);
            if (await FindPortClashAsync(workload, workload.AgentId, ct) is { } clash)
            {
                throw new ValidationException(nameof(CreateWorkloadRequest.Ports), clash);
            }
        }

        workload.Env = ResolveEnv(request.Env, []);
        workload.RestartDailyAtMinutes = WorkloadValidation.ValidateRestartTime(request.RestartDailyAtMinutes);
        await repository.AddAsync(workload, ct);
        await ProvisionDefaultRouteAsync(workload, WorkloadValidation.ValidateHttpPort(request.HttpPort), ct);
        return await MapAsync(workload, AccessLevel.Configure, ct);
    }

    public async Task<WorkloadResponse> UpdateAsync(Caller caller, string id, UpdateWorkloadRequest request, CancellationToken ct = default)
    {
        var (workload, level) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);
        workload.GroupId = await ResolveGroupAsync(request.GroupId, ct);

        if (workload.Kind == WorkloadKind.Container)
        {
            workload.Image = WorkloadValidation.ValidateImage(request.Image);
            WorkloadValidation.ValidateReplicas(request.Replicas);
            WorkloadValidation.ValidatePorts(request.Ports);
            WorkloadValidation.ValidateResources(request.MemoryLimitMb, request.CpuLimit, request.MemoryReserveMb, request.CpuReserve);
            WorkloadValidation.ValidateMounts(request.Mounts);
            // Both of these mean one node, and one node means one instance: a named volume is on exactly
            // one machine, and a chosen node is a choice of one.
            workload.Replicas = WorkloadValidation.SingleInstance(request.Placement, request.Mounts) ? 1 : request.Replicas;
            workload.Placement = request.Placement;
            workload.NodeId = await ValidatePlacementAsync(request.Placement, request.Mode, request.NodeId, ct);
            workload.Ports = WorkloadValidation.PublishedPorts(request.Ports);
            workload.Mode = request.Mode;
            workload.MemoryLimitMb = request.MemoryLimitMb;
            workload.CpuLimit = request.CpuLimit;
            workload.MemoryReserveMb = request.MemoryReserveMb;
            workload.CpuReserve = request.CpuReserve;
            workload.Mounts = [.. request.Mounts];
            WorkloadValidation.ApplyHealth(workload, request.HealthCommand, request.HealthIntervalSeconds, request.HealthTimeoutSeconds, request.HealthRetries, request.HealthStartPeriodSeconds);
        }
        else
        {
            workload.Command = WorkloadValidation.ValidateCommand(request.Command);
            workload.Args = [.. request.Args];
            workload.WorkingDirectory = WorkloadValidation.Blank(request.WorkingDirectory);
            workload.RestartPolicy = request.RestartPolicy;
            workload.StopGraceSeconds = WorkloadValidation.ValidateStopGrace(request.StopGraceSeconds);
            workload.StopCommand = WorkloadValidation.Blank(request.StopCommand);
            workload.ManagedDirectory = request.ManagedDirectory;
            workload.Source = ValidateSource(request.Source, workload.Source, workload.ManagedDirectory);
            // Normalize before validating — a native row carries no target port, so it would fail the
            // 1..65535 check on a field the operator was never asked for.
            workload.Ports = WorkloadValidation.NativePorts(request.Ports);
            WorkloadValidation.ValidatePorts(workload.Ports);
            if (await FindPortClashAsync(workload, workload.AgentId, ct) is { } clash)
            {
                throw new ValidationException(nameof(CreateWorkloadRequest.Ports), clash);
            }
        }

        workload.Env = ResolveEnv(request.Env, workload.Env);
        workload.RestartDailyAtMinutes = WorkloadValidation.ValidateRestartTime(request.RestartDailyAtMinutes);
        workload.UpdatedAt = clock.GetUtcNow();
        await repository.UpdateAsync(workload, ct);

        return await MapAsync(workload, level, ct);
    }

    public async Task DeleteAsync(Caller caller, string id, CancellationToken ct = default)
    {
        WorkloadLoader.RequireAdmin(caller);
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);

        // Removing the record without this leaves the service running with nothing left that knows about
        // it — an orphan only findable from the docker CLI.
        await backends.Resolve(workload.Kind).UndeployAsync(deployments.ToDeployment(workload), ct);

        await repository.RemoveAsync(workload, ct);
        await accessGrants.RemoveForTargetAsync(AccessScope.Workload, id, ct);

        // They're already invisible to nginx, but they keep their (hostname, path) — enough to make a
        // workload recreated under the same name silently get no address.
        foreach (var route in (await routes.ListAsync(ct)).Where(r => r.WorkloadId == id))
        {
            await routes.DeleteAsync(route.Id, ct);
        }
    }



    public async Task<IReadOnlyList<AgentResponse>> MoveTargetsAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);
        if (workload.Kind != WorkloadKind.Native)
        {
            throw new ConflictException("Only a native workload runs on an agent.");
        }

        return
        [
            .. (await agents.ListAsync(ct))
                .Where(a => a.Id != workload.AgentId)
                .Select(a => new AgentResponse
                {
                    Id = a.Id,
                    Name = a.Name,
                    Platform = a.Platform,
                    Status = agentConnections.IsOnline(a.Id) ? AgentStatus.Online : AgentStatus.Offline,
                    EnrolledAt = a.EnrolledAt,
                    LastSeenAt = a.LastSeenAt,
                })
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<MoveWorkloadResponse> MoveAsync(Caller caller, string id, string agentId, CancellationToken ct = default)
    {
        // Configure, not Operate: which machine something runs on is a placement decision with port
        // clashes and offline agents behind it, and it writes to the saved config.
        var (workload, level) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);
        if (workload.Kind != WorkloadKind.Native)
        {
            throw new ValidationException(nameof(MoveWorkloadRequest.AgentId),
                "Only agent workloads move between agents — a container's placement is its exposure and node.");
        }

        var target = await RequireAgentAsync(agentId, "That agent is not enrolled.", ct);
        if (workload.AgentId == target.Id)
        {
            throw new ValidationException(nameof(MoveWorkloadRequest.AgentId), $"'{workload.Name}' already runs on {target.Name}.");
        }

        if (await FindPortClashAsync(workload, target.Id, ct) is { } clash)
        {
            throw new ValidationException(nameof(MoveWorkloadRequest.AgentId), clash);
        }

        var previousAgentId = workload.AgentId;
        var running = WorkloadRevisions.Running(workload);
        var backend = backends.Resolve(workload.Kind);

        // Nothing deployed means there's nothing to hand over — the move is only a reassignment.
        var previousOffline = running is not null && previousAgentId is not null && !agentConnections.IsOnline(previousAgentId);
        if (running is not null && previousAgentId is not null && !previousOffline)
        {
            await backend.StopAsync(deployments.ToDeployment(workload, running), ct);
        }

        workload.AgentId = target.Id;
        workload.UpdatedAt = clock.GetUtcNow();
        await repository.UpdateAsync(workload, ct);

        var targetOffline = running is not null && !agentConnections.IsOnline(target.Id);
        if (running is not null && !targetOffline)
        {
            await backend.DeployAsync(deployments.ToDeployment(workload, running), ct);
        }

        return new MoveWorkloadResponse
        {
            Workload = await MapAsync(workload, level, ct),
            PreviousAgentOffline = previousOffline,
            TargetAgentOffline = targetOffline,
        };
    }




    public async Task<IReadOnlyList<WorkloadRevisionResponse>> GetRevisionsAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);
        var current = WorkloadRevisions.Running(workload)?.Number;
        return workload.Revisions
            .OrderByDescending(r => r.Number)
            .Select(r => new WorkloadRevisionResponse
            {
                Number = r.Number,
                DeployedAt = r.DeployedAt,
                IsCurrent = r.Number == current,
                Summary = WorkloadRevisions.SummaryOf(workload.Kind, r),
                Image = r.Image,
                Replicas = r.Replicas,
                Ports = r.Ports.ToList(),
                Placement = r.Placement,
                NodeId = r.NodeId,
                Mode = r.Mode,
                MemoryLimitMb = r.MemoryLimitMb,
                CpuLimit = r.CpuLimit,
                MemoryReserveMb = r.MemoryReserveMb,
                CpuReserve = r.CpuReserve,
                Mounts = r.Mounts.ToList(),
                Command = r.Command,
                Args = r.Args.ToList(),
                WorkingDirectory = r.WorkingDirectory,
                RestartPolicy = r.RestartPolicy,
                StopGraceSeconds = r.StopGraceSeconds,
                StopCommand = r.StopCommand,
                ManagedDirectory = r.ManagedDirectory,
                Source = SourceResponse(r.Source),
                Env = Redacted(r.Env),
                HealthCommand = r.HealthCommand,
                HealthIntervalSeconds = r.HealthIntervalSeconds,
                HealthTimeoutSeconds = r.HealthTimeoutSeconds,
                HealthRetries = r.HealthRetries,
                HealthStartPeriodSeconds = r.HealthStartPeriodSeconds,
            })
            .ToList();
    }

    public async Task<WorkloadResponse> RevertAsync(Caller caller, string id, int revisionNumber, CancellationToken ct = default)
    {
        var (workload, level) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);
        var revision = workload.Revisions.FirstOrDefault(r => r.Number == revisionNumber)
            ?? throw new NotFoundException($"Revision {revisionNumber} not found for workload '{id}'.");

        workload.Image = revision.Image;
        workload.Replicas = revision.Replicas;
        workload.Ports = [.. revision.Ports];
        workload.Placement = revision.Placement;
        workload.NodeId = revision.NodeId;
        workload.Mode = revision.Mode;
        workload.MemoryLimitMb = revision.MemoryLimitMb;
        workload.CpuLimit = revision.CpuLimit;
        workload.MemoryReserveMb = revision.MemoryReserveMb;
        workload.CpuReserve = revision.CpuReserve;
        workload.Mounts = [.. revision.Mounts];
        workload.Command = revision.Command;
        workload.Args = [.. revision.Args];
        workload.WorkingDirectory = revision.WorkingDirectory;
        workload.RestartPolicy = revision.RestartPolicy;
        workload.StopGraceSeconds = revision.StopGraceSeconds;
        workload.StopCommand = revision.StopCommand;
        workload.ManagedDirectory = revision.ManagedDirectory;
        workload.Source = revision.Source.Copy();
        workload.HealthCommand = revision.HealthCommand;
        workload.HealthIntervalSeconds = revision.HealthIntervalSeconds;
        workload.HealthTimeoutSeconds = revision.HealthTimeoutSeconds;
        workload.HealthRetries = revision.HealthRetries;
        workload.HealthStartPeriodSeconds = revision.HealthStartPeriodSeconds;
        workload.Env = [.. revision.Env];
        workload.UpdatedAt = clock.GetUtcNow();

        await repository.UpdateAsync(workload, ct);
        return await MapAsync(workload, level, ct);
    }












    public async Task<IReadOnlyList<RouteResponse>> GetRoutesAsync(Caller caller, string id, CancellationToken ct = default)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.View, ct);
        return [.. (await routes.ListAsync(ct)).Where(r => r.WorkloadId == workload.Id)];
    }





    // Identity (name/kind/agent/network) from the workload; config from a specific revision.

    /// Gives an HTTP container its own address under the platform's root domain. Best-effort by design:
    /// a workload that saved fine must not fail because its address was already taken, and no root
    /// domain configured simply means no automatic address.
    private async Task ProvisionDefaultRouteAsync(Workload workload, int? httpPort, CancellationToken ct)
    {
        if (workload.Kind != WorkloadKind.Container || httpPort is not { } port)
        {
            return;
        }

        var rootDomain = (await platformSettings.GetAsync(ct))?.RootDomain;
        if (DefaultHostname.For(workload.Name, rootDomain) is not { } hostname)
        {
            return;
        }

        try
        {
            await routes.CreateAsync(new CreateRouteRequest
            {
                Hostname = hostname,
                Path = "/",
                Target = RouteTarget.Workload,
                WorkloadId = workload.Id,
                TargetPort = port,
            }, ct);
        }
        catch (ConflictException)
        {
            // Something already answers on that hostname — leave it be.
        }
    }





    private async Task<Dictionary<string, string>> AgentNamesAsync(CancellationToken ct)
        => (await agents.ListAsync(ct)).ToDictionary(a => a.Id, a => a.Name);

    private async Task<Dictionary<string, string>> GroupNamesAsync(CancellationToken ct)
        => (await groups.ListAsync(ct)).ToDictionary(g => g.Id, g => g.Name);

    private async Task<string?> ResolveGroupAsync(string? groupId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        if (await groups.FindByIdAsync(groupId, ct) is null)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.GroupId), "Group not found.");
        }

        return groupId;
    }

    private async Task<WorkloadResponse> MapAsync(Workload w, AccessLevel level, CancellationToken ct)
    {
        var agentName = w.AgentId is null ? null : (await agents.FindByIdAsync(w.AgentId, ct))?.Name;
        var groupName = w.GroupId is null ? null : (await groups.FindByIdAsync(w.GroupId, ct))?.Name;
        return Map(w, agentName, groupName, level);
    }




    private async Task<string> ValidateAgentAsync(string? agentId, CancellationToken ct)
        => (await RequireAgentAsync(agentId, "Select an agent for this workload.", ct)).Id;

    private async Task<Agent> RequireAgentAsync(string? agentId, string message, CancellationToken ct)
    {
        var agent = string.IsNullOrWhiteSpace(agentId) ? null : await agents.FindByIdAsync(agentId, ct);
        return agent ?? throw new ValidationException(nameof(CreateWorkloadRequest.AgentId), message);
    }





    private async Task<string?> ValidatePlacementAsync(WorkloadPlacement placement, WorkloadMode mode, string? nodeId, CancellationToken ct)
    {
        if (placement != WorkloadPlacement.Node)
        {
            return null;
        }

        // Pinning to one node and Global running on all of them is asking for nothing coherent.
        if (mode == WorkloadMode.Global)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Placement),
                "A global workload already runs on every node, so it can't also be pinned to one.");
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.NodeId), "Pick the node this workload runs on.");
        }

        var known = await nodes.ListAsync(ct);
        var node = known.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.NodeId), "That node is not in the cluster.");
        }

        // Pinning a container to an agent yields "no suitable node" forever: an agent runs processes,
        // not containers, so no swarm node ever matches the constraint.
        if (node.Backend != NodeBackendKind.Swarm)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.NodeId),
                "Only swarm nodes can run containers. Pick a swarm node, or target an agent instead.");
        }

        return nodeId;
    }


    /// Nothing else catches this: two processes on one machine can't share a port, and unlike swarm
    /// there's no scheduler to refuse the second one — it just fails to bind at runtime. Takes the agent
    /// separately from the workload so a move can ask about the agent it's headed for.
    private async Task<string?> FindPortClashAsync(Workload workload, string? agentId, CancellationToken ct)
    {
        if (workload.Ports.Count == 0 || agentId is null)
        {
            return null;
        }

        var taken = (await repository.ListAsync(ct))
            .Where(w => w.Kind == WorkloadKind.Native && w.AgentId == agentId && w.Id != workload.Id)
            .SelectMany(w => w.Ports.Select(p => (p.Published, p.Protocol, w.Name)))
            .ToList();

        foreach (var port in workload.Ports)
        {
            var clash = taken.FirstOrDefault(t => t.Published == port.Published && t.Protocol == port.Protocol);
            if (clash.Name is not null)
            {
                return $"Port {port.Published}/{port.Protocol.ToString().ToLowerInvariant()} is already used by '{clash.Name}' on that agent.";
            }
        }

        return null;
    }

    /// A source writes files, so it needs a directory the agent owns; and it needs somewhere to fetch
    /// from. Both are refused up front rather than failing minutes into an acquire.
    /// A source writes files, so it needs a directory the agent owns, and enough detail to fetch with.
    /// Both are refused up front rather than failing minutes into an acquire. A blank Steam password
    /// carries the stored ciphertext forward untouched, so editing anything else leaves it alone.
    private WorkloadSource ValidateSource(WorkloadSourceRequest? request, WorkloadSource current, bool managedDirectory)
    {
        var kind = request?.Kind ?? SourceKind.None;

        if (kind == SourceKind.None)
        {
            return new WorkloadSource();
        }

        if (!managedDirectory)
        {
            throw new ValidationException(nameof(CreateWorkloadRequest.Source),
                "A source needs the agent to own the workload's directory — turn that on first.");
        }

        var source = new WorkloadSource
        {
            Kind = kind,
            SteamBranch = WorkloadValidation.Blank(request!.SteamBranch),
            SteamUsername = WorkloadValidation.Blank(request.SteamUsername),
            SteamPasswordEnc = string.IsNullOrEmpty(request.SteamPassword)
                ? current.SteamPasswordEnc
                : protector.Protect(request.SteamPassword),
        };

        if (kind == SourceKind.Zip)
        {
            var url = request.Url?.Trim();
            if (string.IsNullOrEmpty(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException(nameof(WorkloadSourceRequest.Url), "Enter an http or https URL to fetch from.");
            }

            source.Url = url;
            return source;
        }

        if (request.SteamAppId is not > 0)
        {
            throw new ValidationException(nameof(WorkloadSourceRequest.SteamAppId), "Enter the Steam app id of the dedicated server.");
        }

        // A username with no password can never log in, and Steam's anonymous login ignores both.
        if (source.SteamUsername is not null && string.IsNullOrEmpty(source.SteamPasswordEnc))
        {
            throw new ValidationException(nameof(WorkloadSourceRequest.SteamPassword), "Enter the password for that Steam account.");
        }

        source.SteamAppId = request.SteamAppId;
        return source;
    }


    private static WorkloadSourceResponse? SourceResponse(WorkloadSource source) => source.Kind == SourceKind.None ? null : new WorkloadSourceResponse
    {
        Kind = source.Kind,
        Url = source.Url,
        SteamAppId = source.SteamAppId,
        SteamBranch = source.SteamBranch,
        SteamUsername = source.SteamUsername,
        HasSteamPassword = !string.IsNullOrEmpty(source.SteamPasswordEnc),
    };









    /// Turns incoming env into what gets stored: plain vars pass through, secret vars are encrypted, and
    /// a secret that hasn't actually changed — arriving blank, or arriving as the same plaintext the
    /// read handed out — keeps the ciphertext already on file.
    ///
    /// Carrying the old ciphertext through *byte for byte* is what keeps pending-changes honest. GCM uses
    /// a fresh nonce per call, so re-encrypting an unchanged secret would produce different bytes, and
    /// WorkloadConfigSignature would read that as a config change on every save.
    private List<EnvVar> ResolveEnv(IEnumerable<EnvVar> incoming, IReadOnlyList<EnvVar> existing)
    {
        var resolved = new List<EnvVar>();

        foreach (var variable in incoming)
        {
            if (!variable.Secret)
            {
                resolved.Add(variable);
                continue;
            }

            var current = existing.FirstOrDefault(e => e.Secret && e.Key == variable.Key);

            if (!string.IsNullOrEmpty(variable.Value))
            {
                resolved.Add(current is not null && protector.Unprotect(current.Value) == variable.Value
                    ? current
                    : variable with { Value = protector.Protect(variable.Value) });
                continue;
            }

            resolved.Add(current
                ?? throw new ValidationException(nameof(CreateWorkloadRequest.Env), $"A value is required for secret '{variable.Key}'."));
        }

        return resolved;
    }


    private static List<EnvVar> Redacted(IEnumerable<EnvVar> env) =>
        env.Select(v => v.Secret ? v with { Value = string.Empty } : v).ToList();

    private static WorkloadResponse Map(Workload w, string? agentName, string? groupName, AccessLevel access)
    {
        var running = WorkloadRevisions.Running(w);
        return new WorkloadResponse
        {
            Id = w.Id,
            Name = w.Name,
            GroupId = w.GroupId,
            GroupName = groupName,
            DesiredState = w.DesiredState,
            Target = w.Target,
            Kind = w.Kind,
            AgentId = w.AgentId,
            AgentName = agentName,
            Image = string.IsNullOrEmpty(w.Image) ? null : w.Image,
            Replicas = w.Replicas,
            Ports = w.Ports.ToList(),
            Placement = w.Placement,
            NodeId = w.NodeId,
            PlacedNodeId = w.PlacedNodeId,
            ServiceName = w.Kind == WorkloadKind.Container ? SwarmNaming.ServiceName(w.Name) : null,
            Mode = w.Mode,
            MemoryLimitMb = w.MemoryLimitMb,
            CpuLimit = w.CpuLimit,
            MemoryReserveMb = w.MemoryReserveMb,
            CpuReserve = w.CpuReserve,
            Mounts = w.Mounts.ToList(),
            Command = w.Command,
            Args = w.Args.ToList(),
            WorkingDirectory = w.WorkingDirectory,
            RestartPolicy = w.RestartPolicy,
            StopGraceSeconds = w.StopGraceSeconds,
            StopCommand = w.StopCommand,
            ManagedDirectory = w.ManagedDirectory,
            Source = SourceResponse(w.Source),
            RestartDailyAtMinutes = w.RestartDailyAtMinutes,
            Env = Redacted(w.Env),
            HealthCommand = w.HealthCommand,
            HealthIntervalSeconds = w.HealthIntervalSeconds,
            HealthTimeoutSeconds = w.HealthTimeoutSeconds,
            HealthRetries = w.HealthRetries,
            HealthStartPeriodSeconds = w.HealthStartPeriodSeconds,
            HasPendingChanges = WorkloadRevisions.HasPendingChanges(w),
            CurrentRevision = running?.Number,
            IsDeploying = WorkloadRevisions.Newest(w) is { Applied: false },
            Access = access,
            CanDeploy = DeployPermission.Allowed(access, running is not null, WorkloadRevisions.HasPendingChanges(w)),
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt,
        };
    }

    // Lifecycle lives in its own class; these forward to it so endpoints and clients keep one surface.
    // The interface is still wide — splitting it too is a follow-up, and costs ~123 test call sites.

    public Task<WorkloadRuntimeStatus> DeployAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.DeployAsync(caller, id, ct);

    public Task<WorkloadRuntimeStatus> ScaleAsync(Caller caller, string id, int replicas, CancellationToken ct = default)
        => lifecycle.ScaleAsync(caller, id, replicas, ct);

    public Task<WorkloadRuntimeStatus> RestartAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.RestartAsync(caller, id, ct);

    public Task<WorkloadRuntimeStatus> UpdateAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.UpdateAsync(caller, id, ct);

    public Task SendConsoleAsync(Caller caller, string id, string text, CancellationToken ct = default)
        => lifecycle.SendConsoleAsync(caller, id, text, ct);

    public Task<WorkloadRuntimeStatus> StartAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.StartAsync(caller, id, ct);

    public Task StopAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.StopAsync(caller, id, ct);

    public Task UndeployAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.UndeployAsync(caller, id, ct);

    public Task<WorkloadDriftResponse> GetDriftAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.GetDriftAsync(caller, id, ct);

    public Task<WorkloadRuntimeStatus> GetStatusAsync(Caller caller, string id, CancellationToken ct = default)
        => lifecycle.GetStatusAsync(caller, id, ct);

    public Task<IReadOnlyDictionary<string, WorkloadRuntimeStatus>> GetStatusesAsync(Caller caller, CancellationToken ct = default)
        => lifecycle.GetStatusesAsync(caller, ct);

    public Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(Caller caller, string id, int tail, CancellationToken ct = default)
        => lifecycle.GetLogsAsync(caller, id, tail, ct);

    public Task<bool> SettleRevisionAsync(string workloadId, WorkloadRuntimeStatus status, CancellationToken ct = default)
        => lifecycle.SettleRevisionAsync(workloadId, status, ct);
}
