using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

internal static class WorkloadRevisions
{
    public const int RevisionHistory = 3;
    public static WorkloadRevision? Newest(Workload w)
        => w.Revisions.Count == 0 ? null : w.Revisions.OrderByDescending(r => r.Number).First();
    /// not always the newest, a rolled-back deploy leaves a newer revision that never took
    public static WorkloadRevision? Running(Workload w)
        => w.Revisions.Where(r => r.Applied).OrderByDescending(r => r.Number).FirstOrDefault();
    /// false if never deployed, which is why the deploy check asks about the revision too
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

        // copying field by field used to drop health settings and leave scaled workloads stuck pending
        running.Replicas = replicas;
    }
    /// only acts on an unsettled revision, so repeat calls are harmless (swarm says rollback_completed for ages)
    public static bool Settle(Workload w, WorkloadRuntimeStatus status)
    {
        if (Newest(w) is not { Applied: false } pending)
        {
            return false;
        }

        // agent deploy is done when the call returns, nothing else will settle it
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

                // backend put the old spec back so this never ran, dropping it brings back the pending banner for a retry
                case RolloutOutcome.Reverted:
                    w.Revisions = [.. w.Revisions.Where(r => r.Number != pending.Number)];
                    break;

                default:
                    return false;
            }
        }

        return true;
    }
    /// first placement we see sticks forever, re-learning after losing the node would bless an empty volume
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
