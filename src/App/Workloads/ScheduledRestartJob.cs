using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public sealed class ScheduledRestartJob(
    IWorkloadRepository repository,
    WorkloadLifecycleService lifecycle,
    TimeProvider clock) : IScheduledJob
{
    public string Name => "workload-restarts";

    /// finer than the grace window so a due restart is never missed
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

            // recorded before the attempt so a throwing restart isn't retried every tick
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
                // one wedged workload mustn't block the rest, App has no logger so failures go up to the runner
                (failures ??= []).Add(new InvalidOperationException($"'{workload.Name}' did not restart.", ex));
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
