using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// Bounces workloads that ask for a daily restart. Long-running game servers leak and fragment; a
/// restart in the quiet hours is cheaper than one at peak.
public sealed class ScheduledRestartJob(
    IWorkloadRepository repository,
    WorkloadLifecycleService lifecycle,
    TimeProvider clock) : IScheduledJob
{
    public string Name => "workload-restarts";

    /// Finer than the grace window, so a due restart is never missed between ticks.
    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = clock.GetLocalNow();
        List<Exception>? failures = null;

        foreach (var workload in await repository.ListAsync(ct))
        {
            if (workload.DesiredState != WorkloadDesiredState.Running
                || !workload.Revisions.Exists(r => r.Applied)
                || !ScheduledRestart.IsDue(workload.RestartDailyAtMinutes, now, workload.LastScheduledRestartAt))
            {
                continue;
            }

            // Recorded before the attempt, not after: a restart that throws must not be retried on every
            // tick for the rest of the grace window.
            workload.LastScheduledRestartAt = now;
            await repository.UpdateAsync(workload, ct);

            try
            {
                await lifecycle.RestartAsync(Caller.System, workload.Id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One wedged workload must not cost the others their restart. App has no logger, so the
                // failures ride out to the job runner, which does.
                (failures ??= []).Add(new InvalidOperationException($"'{workload.Name}' did not restart.", ex));
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
