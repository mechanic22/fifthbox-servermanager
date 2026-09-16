using System.Collections.Concurrent;
using FifthBox.ServerManager.App.Common;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Host.Infrastructure;

public interface IScheduledJobs
{
    IReadOnlyList<ScheduledJobStatus> List();

    /// throws if it's already running
    Task<ScheduledJobStatus> RunNowAsync(string name, CancellationToken ct = default);
}

/// a job that throws just gets logged and tried again next time
public sealed class ScheduledJobRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledJobRunner> logger,
    TimeProvider clock,
    IConfiguration config) : BackgroundService, IScheduledJobs
{
    private readonly TimeSpan _tick = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Jobs:TickSeconds", 30)));
    private readonly ConcurrentDictionary<string, JobState> _state = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ScheduledJobStatus> List()
    {
        using var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetServices<IScheduledJob>()
            .Select(Describe)
            .OrderBy(j => j.Name, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ScheduledJobStatus> RunNowAsync(string name, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IScheduledJob>()
            .FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"Job '{name}' not found.");

        await RunAsync(job, ct);
        return Describe(job);
    }

    private ScheduledJobStatus Describe(IScheduledJob job)
    {
        var state = _state.GetValueOrDefault(job.Name);
        return new ScheduledJobStatus
        {
            Name = job.Name,
            IntervalSeconds = (int)job.Interval.TotalSeconds,
            LastRunAt = state?.LastRunAt,
            NextRunAt = state?.LastRunAt is { } last ? last + job.Interval : null,
            Running = state?.Running ?? false,
            LastError = state?.LastError,
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tick);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                foreach (var job in scope.ServiceProvider.GetServices<IScheduledJob>())
                {
                    if (IsDue(job))
                    {
                        await RunAsync(job, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled job sweep failed; retrying next tick");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // never run means due now, so a daily job doesn't wait a day to prove it works
    private bool IsDue(IScheduledJob job)
    {
        var state = _state.GetValueOrDefault(job.Name);
        return state is null || (!state.Running && clock.GetUtcNow() - state.LastRunAt >= job.Interval);
    }

    private async Task RunAsync(IScheduledJob job, CancellationToken ct)
    {
        var state = _state.GetOrAdd(job.Name, _ => new JobState());
        lock (state)
        {
            if (state.Running)
            {
                throw new ConflictException($"Job '{job.Name}' is already running.");
            }

            state.Running = true;
        }

        try
        {
            await job.RunAsync(ct);
            state.LastError = null;
        }
        catch (Exception ex)
        {
            state.LastError = ex.Message;
            logger.LogError(ex, "Scheduled job {Job} failed", job.Name);
        }
        finally
        {
            state.LastRunAt = clock.GetUtcNow();
            state.Running = false;
        }
    }

    private sealed class JobState
    {
        public DateTimeOffset? LastRunAt { get; set; }
        public bool Running { get; set; }
        public string? LastError { get; set; }
    }
}
