using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

/// docker can't ask for the lines before a point, so earlier means a bigger tail
public sealed class WorkloadLogsView
{
    private const int MaxLines = 2_500;
    private static readonly int[] TailSteps = [500, 2_000];

    private IReadOnlyList<WorkloadLogLine> _previous = [];
    private List<WorkloadLogLine> _current = [];

    public int Tail { get; private set; } = TailSteps[0];

    public bool Loaded { get; private set; }

    /// a new list each change, the viewer tells new output by the reference
    public IReadOnlyList<WorkloadLogLine> Lines { get; private set; } = [];

    /// fewer lines than asked for means that's all there is
    public bool CanLoadEarlier => Tail < TailSteps[^1] && _current.Count >= Tail;

    public int NextTail => TailSteps.FirstOrDefault(step => step > Tail, Tail);

    public void SetCurrent(IReadOnlyList<WorkloadLogLine> lines, int tail)
    {
        _current = [.. lines];
        Tail = tail;
        Loaded = true;
        Rebuild();
    }

    public void SetPrevious(IReadOnlyList<WorkloadLogLine> lines)
    {
        _previous = lines;
        Rebuild();
    }

    public void Append(IReadOnlyList<WorkloadLogLine> lines)
    {
        _current.AddRange(lines);
        Rebuild();
    }

    private void Rebuild() => Lines = [.. _previous.Concat(_current).TakeLast(MaxLines)];
}
