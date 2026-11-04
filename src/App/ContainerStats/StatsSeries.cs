using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.ContainerStats;

/// every sample for 30 minutes, then one averaged point a minute for 24 hours. about 1,600 points at most
public sealed class StatsSeries
{
    public static readonly TimeSpan RawWindow = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan FullWindow = TimeSpan.FromHours(24);

    private readonly Queue<StatsPoint> _raw = new();
    private readonly List<Minute> _minutes = [];

    public long LastAt { get; private set; }

    public void Add(DateTimeOffset at, double cpu, long memory)
    {
        var now = at.ToUnixTimeSeconds();
        _raw.Enqueue(new StatsPoint(now, cpu, memory));
        LastAt = now;

        var rawCutoff = now - (long)RawWindow.TotalSeconds;
        while (_raw.TryPeek(out var oldest) && oldest.At < rawCutoff)
        {
            Fold(_minutes, _raw.Dequeue());
        }

        var fullCutoff = now - (long)FullWindow.TotalSeconds;
        _minutes.RemoveAll(m => m.At < fullCutoff);
    }

    public List<StatsPoint> Read(StatsHistoryRange range)
    {
        if (range == StatsHistoryRange.ThirtyMinutes)
        {
            return [.. _raw];
        }

        // recent samples thinned too, or the last half hour would be drawn in finer detail than the rest
        var minutes = new List<Minute>(_minutes);
        foreach (var point in _raw)
        {
            Fold(minutes, point);
        }

        return minutes.ConvertAll(m => m.ToPoint());
    }

    private static void Fold(List<Minute> minutes, StatsPoint point)
    {
        var at = point.At - (point.At % 60);
        if (minutes.Count > 0 && minutes[^1].At == at)
        {
            minutes[^1] = minutes[^1].With(point);
            return;
        }

        minutes.Add(new Minute(at, point.Cpu, point.Memory, 1));
    }

    private readonly record struct Minute(long At, double CpuSum, long MemorySum, int Count)
    {
        public Minute With(StatsPoint point) => new(At, CpuSum + point.Cpu, MemorySum + point.Memory, Count + 1);

        public StatsPoint ToPoint() => new(At, Math.Round(CpuSum / Count, 2), MemorySum / Count);
    }
}
