namespace FifthBox.ServerManager.App.Tests;

/// timers only fire on Advance, so timeout paths test without waiting
internal sealed class ManualClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, owner: this, dueAt: DueFrom(dueTime));
        lock (_timers)
        {
            _timers.Add(timer);
        }
        return timer;
    }

    private DateTimeOffset DueFrom(TimeSpan dueTime)
        => dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : _now + dueTime;

    public void Advance(TimeSpan by)
    {
        _now += by;
        List<ManualTimer> due;
        lock (_timers)
        {
            due = [.. _timers.Where(t => t.DueAt <= _now)];
            _timers.RemoveAll(due.Contains);
        }
        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt, ManualClock owner) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } = dueAt;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = owner.DueFrom(dueTime);
            return true;
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
