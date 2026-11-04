using System.Text.Json;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.App.ContainerStats;

public sealed class ContainerStatsOptions
{
    /// null on a throwaway encryption key, nothing can be signed so everything gets rejected
    public string? SigningKey { get; set; }

    public string ServiceName { get; set; } = "fbsm-stats";

    /// no tag, it runs the same version as the Host
    public string Image { get; set; } = "docker.5thbox.com/fbsm/stats";

    /// null reaches the Host by service name on the overlay
    public string? HostUrl { get; set; }
}

public interface IContainerStatsIngest
{
    /// checks the signature before it touches the body, this endpoint is open to the internet
    Task IngestAsync(byte[] body, string? timestamp, string? signature, CancellationToken ct = default);
}

public sealed class ContainerStatsIngest(
    IContainerStatsStore store,
    IStatsHistoryStore history,
    IClusterState cluster,
    IContainerStatsNotifier notifier,
    IOptions<ContainerStatsOptions> options,
    TimeProvider clock) : IContainerStatsIngest
{
    public const int MaxBodyBytes = 256 * 1024;
    public const int MaxEntriesPerReport = 1_000;
    private const int MaxTaskIdLength = 64;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task IngestAsync(byte[] body, string? timestamp, string? signature, CancellationToken ct = default)
    {
        if (options.Value.SigningKey is not { Length: > 0 } key
            || !ContainerStatsSignature.IsValid(key, timestamp, signature, body, clock.GetUtcNow()))
        {
            throw new UnauthorizedException("This report isn't signed correctly.");
        }

        var report = Parse(body);
        var accepted = report.Containers.Where(IsSane).Take(MaxEntriesPerReport).ToList();
        var machine = report.Machine is { } m && IsSane(m) ? m : null;

        store.Record(accepted);
        foreach (var (workloadId, task, entry) in ContainerStatsRouting.Matches(cluster.Statuses, DistinctTasks(accepted)))
        {
            history.RecordReplica(workloadId, task, entry);
        }

        if (machine is not null)
        {
            history.RecordMachine(machine);
        }

        await notifier.NotifyAsync(new ContainerStatsReport { Containers = DistinctTasks(accepted), Machine = machine }, ct);
    }

    // keys on the id, so one report repeating an id can't throw
    private static List<ContainerStatsEntry> DistinctTasks(List<ContainerStatsEntry> entries) =>
        entries.DistinctBy(e => e.TaskId, StringComparer.Ordinal).ToList();

    private static ContainerStatsReport Parse(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<ContainerStatsReport>(body, Json)
                ?? throw new ValidationException("report", "The report is empty.");
        }
        catch (JsonException)
        {
            throw new ValidationException("report", "The report isn't valid.");
        }
    }

    private static bool IsSane(MachineStats stats) =>
        IsId(stats.NodeId)
        && stats.CpuPercent is null or (>= 0 and <= 100)
        && stats.CpuCores is null or > 0
        && stats.MemoryUsedBytes >= 0
        && stats.MemoryTotalBytes >= 0
        && stats.DiskFreeBytes is null or >= 0
        && stats.DiskTotalBytes is null or >= 0;

    private static bool IsId(string? id) =>
        id is { Length: > 0 and <= MaxTaskIdLength } && id.All(char.IsAsciiLetterOrDigit);

    private static bool IsSane(ContainerStatsEntry entry) =>
        IsId(entry.TaskId)
        && double.IsFinite(entry.CpuPercent)
        && entry.CpuPercent >= 0
        && entry.MemoryBytes >= 0
        && entry.MemoryLimitBytes is null or > 0;
}
