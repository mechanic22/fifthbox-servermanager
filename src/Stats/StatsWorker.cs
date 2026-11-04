using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Stats;

public sealed class StatsWorker(
    StatsCollector collector,
    MachineReader machines,
    IReportSender sender,
    IOptions<StatsOptions> options,
    ILogger<StatsWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
        do
        {
            try
            {
                var machine = machines.Read();
                var report = await collector.CollectAsync(machine?.MemoryTotalBytes, stoppingToken);
                await sender.SendAsync(report with { Machine = machine }, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // the Host restarting is normal, the next tick has fresh numbers anyway
                log.LogWarning("Couldn't report stats: {Reason}", ex.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
