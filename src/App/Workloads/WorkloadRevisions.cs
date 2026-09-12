using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// The revision history rules: what counts as a change worth snapshotting, which snapshot is the running
/// one, and what a fresh status says about a rollout that has settled. Static because none of it needs
/// anything beyond the workload in front of it.
internal static class WorkloadRevisions
{
    public const int RevisionHistory = 3;
    public static WorkloadRevision? Newest(Workload w)
        => w.Revisions.Count == 0 ? null : w.Revisions.OrderByDescending(r => r.Number).First();
    /// The revision the backend is actually holding. Not always the newest: a deploy swarm rolled back
    /// leaves a newer revision that never took, and treating that as running is how the platform ends up
    /// describing config the cluster isn't using.
    public static WorkloadRevision? Running(Workload w)
        => w.Revisions.Where(r => r.Applied).OrderByDescending(r => r.Number).FirstOrDefault();
    /// Saved config differs from what's running. False for a workload that was never deployed — there's
    /// nothing to differ from — which is why the deploy check asks about the revision separately.
    public static bool HasPendingChanges(Workload w)
        => Running(w) is { } running && WorkloadConfigSignature.Of(w) != WorkloadConfigSignature.Of(running);
    public static bool RecordRevisionIfChanged(Workload w, TimeProvider clock)
    {
        var newest = Newest(w);
        if (newest is not null && WorkloadConfigSignature.Of(w) == WorkloadConfigSignature.Of(newest))
        {
            return false;
        }

        var revision = new WorkloadRevision
        {
            Number = (newest?.Number ?? 0) + 1,
            DeployedAt = clock.GetUtcNow(),
            Applied = false,
            Image = w.Image,
            Replicas = w.Replicas,
            Ports = [.. w.Ports],
            Placement = w.Placement,
            NodeId = w.NodeId,
            Mode = w.Mode,
            MemoryLimitMb = w.MemoryLimitMb,
            CpuLimit = w.CpuLimit,
            MemoryReserveMb = w.MemoryReserveMb,
            CpuReserve = w.CpuReserve,
            Mounts = [.. w.Mounts],
            Command = w.Command,
            Args = [.. w.Args],
            WorkingDirectory = w.WorkingDirectory,
            RestartPolicy = w.RestartPolicy,
            StopGraceSeconds = w.StopGraceSeconds,
            StopCommand = w.StopCommand,
            ManagedDirectory = w.ManagedDirectory,
            Source = w.Source.Copy(),
            Env = [.. w.Env],
            HealthCommand = w.HealthCommand,
            HealthIntervalSeconds = w.HealthIntervalSeconds,
            HealthTimeoutSeconds = w.HealthTimeoutSeconds,
            HealthRetries = w.HealthRetries,
            HealthStartPeriodSeconds = w.HealthStartPeriodSeconds,
        };

        w.Revisions = w.Revisions
            .Append(revision)
            .OrderByDescending(r => r.Number)
            .Take(RevisionHistory)
            .OrderBy(r => r.Number)
            .ToList();
        return true;
    }
    public static void SyncRunningReplicas(Workload w, int replicas)
    {
        var running = Running(w);
        if (running is null || running.Replicas == replicas)
        {
            return;
        }

        // Copying field by field here used to silently drop the health-check settings, which the config
        // signature reads — so scaling a workload with a probe left it permanently "pending changes".
        running.Replicas = replicas;
    }
    public static bool Settle(Workload w, WorkloadRuntimeStatus status)
    {
        if (Newest(w) is not { Applied: false } pending)
        {
            return false;
        }

        // An agent deploy is finished when the call returns; there is no rollout, and nothing else will
        // ever come back to settle it.
        if (w.Kind == WorkloadKind.Native)
        {
            pending.Applied = true;
        }
        else
        {
            switch (Rollout.Of(status))
            {
                case RolloutOutcome.Applied:
                    pending.Applied = true;
                    break;

                // The backend put the previous spec back, so this revision never ran. Dropping it rather
                // than flagging it means the pending-changes banner reappears and offers the retry.
                case RolloutOutcome.Reverted:
                    w.Revisions = [.. w.Revisions.Where(r => r.Number != pending.Number)];
                    break;

                default:
                    return false;
            }
        }

        return true;
    }
    /// Reconcile the newest revision against what the backend did with it. Only ever acts on a revision
    /// that hasn't settled yet, which is what makes repeated calls on the same status harmless — swarm
    /// keeps reporting "rollback_completed" long after the rollback.
    /// A named volume only exists on the node its task landed on, so the first placement we see becomes
    /// the placement forever. Learned once and never revised: if the node is gone, re-learning would
    /// quietly bless a fresh empty volume as the real one.
    public static bool RememberPlacement(Workload w, WorkloadRuntimeStatus status)
    {
        if (w.Kind != WorkloadKind.Container
            || w.Placement != WorkloadPlacement.Auto
            || w.PlacedNodeId is not null
            || !WorkloadValidation.HasNamedVolume(w.Mounts))
        {
            return false;
        }

        var node = status.Tasks
            .Where(t => t.State == WorkloadTaskState.Running && t.NodeId is not null)
            .OrderByDescending(t => t.Since)
            .Select(t => t.NodeId)
            .FirstOrDefault();

        if (node is null)
        {
            return false;
        }

        w.PlacedNodeId = node;
        return true;
    }
    public static string SummaryOf(WorkloadKind kind, WorkloadRevision r) => kind == WorkloadKind.Container
        ? $"{r.Image} ×{r.Replicas}"
        : $"{r.Command} {string.Join(' ', r.Args)}".Trim();
}
