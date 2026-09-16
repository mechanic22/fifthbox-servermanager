using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent;

public sealed class LogBuffer(int capacity)
{
    private readonly Queue<WorkloadLogLine> _lines = new(capacity);
    private readonly Lock _gate = new();

    public int Capacity { get; } = Math.Max(1, capacity);

    public void Add(WorkloadLogLine line)
    {
        lock (_gate)
        {
            if (_lines.Count == Capacity)
            {
                _lines.Dequeue();
            }

            _lines.Enqueue(line);
        }
    }

    /// oldest first
    public IReadOnlyList<WorkloadLogLine> Tail(int tail)
    {
        lock (_gate)
        {
            if (tail <= 0 || _lines.Count == 0)
            {
                return [];
            }

            return tail >= _lines.Count ? [.. _lines] : [.. _lines.Skip(_lines.Count - tail)];
        }
    }
}
