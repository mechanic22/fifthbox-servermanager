using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Tests.ContainerStats;

[TestClass]
public class StatsHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static StatsSeries Filled(TimeSpan span, TimeSpan every, double cpu = 10)
    {
        var series = new StatsSeries();
        for (var at = Start; at <= Start + span; at += every)
        {
            series.Add(at, cpu, 100);
        }

        return series;
    }

    [TestMethod]
    public void The_30_minute_view_keeps_every_sample()
    {
        var points = Filled(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(10)).Read(StatsHistoryRange.ThirtyMinutes);

        Assert.HasCount(61, points);
    }

    [TestMethod]
    public void Samples_older_than_30_minutes_leave_the_30_minute_view()
    {
        var points = Filled(TimeSpan.FromHours(2), TimeSpan.FromSeconds(10)).Read(StatsHistoryRange.ThirtyMinutes);

        Assert.AreEqual(Start.AddHours(2).AddMinutes(-30).ToUnixTimeSeconds(), points[0].At);
        Assert.HasCount(181, points);
    }

    [TestMethod]
    public void The_24_hour_view_has_one_point_a_minute()
    {
        var points = Filled(TimeSpan.FromHours(2), TimeSpan.FromSeconds(10)).Read(StatsHistoryRange.Day);

        Assert.HasCount(121, points);
        Assert.IsTrue(points.All(p => p.At % 60 == 0));
    }

    [TestMethod]
    public void A_thinned_point_is_the_average_of_its_minute()
    {
        var series = new StatsSeries();
        series.Add(Start, 10, 100);
        series.Add(Start.AddSeconds(10), 20, 300);

        var point = series.Read(StatsHistoryRange.Day).Single();

        Assert.AreEqual(15, point.Cpu);
        Assert.AreEqual(200, point.Memory);
    }

    [TestMethod]
    public void Nothing_older_than_24_hours_is_kept()
    {
        var points = Filled(TimeSpan.FromHours(25), TimeSpan.FromMinutes(1)).Read(StatsHistoryRange.Day);

        Assert.AreEqual(Start.AddHours(1).ToUnixTimeSeconds(), points[0].At);
        Assert.HasCount(1441, points);
    }

    [TestMethod]
    public void A_replacement_task_in_the_same_slot_carries_on_the_same_line()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new StatsHistoryStore(clock);

        store.RecordReplica("w", new WorkloadTask { Id = "old", Slot = 1 }, new ContainerStatsEntry { TaskId = "old" });
        clock.Now = Start.AddSeconds(10);
        store.RecordReplica("w", new WorkloadTask { Id = "new", Slot = 1 }, new ContainerStatsEntry { TaskId = "new" });

        Assert.HasCount(2, store.ForWorkload("w", StatsHistoryRange.ThirtyMinutes).Single().Points);
    }

    [TestMethod]
    public void A_global_services_tasks_are_told_apart_by_node()
    {
        var store = new StatsHistoryStore(new FakeTimeProvider(Start));

        store.RecordReplica("w", new WorkloadTask { Id = "a", Slot = 0, NodeId = "n1" }, new ContainerStatsEntry { TaskId = "a" });
        store.RecordReplica("w", new WorkloadTask { Id = "b", Slot = 0, NodeId = "n2" }, new ContainerStatsEntry { TaskId = "b" });

        Assert.HasCount(2, store.ForWorkload("w", StatsHistoryRange.ThirtyMinutes));
    }

    [TestMethod]
    public void One_workloads_history_never_shows_on_another()
    {
        var store = new StatsHistoryStore(new FakeTimeProvider(Start));

        store.RecordReplica("w1", new WorkloadTask { Id = "a", Slot = 1 }, new ContainerStatsEntry { TaskId = "a" });

        Assert.IsEmpty(store.ForWorkload("w2", StatsHistoryRange.ThirtyMinutes));
    }

    [TestMethod]
    public void The_series_count_is_capped()
    {
        var store = new StatsHistoryStore(new FakeTimeProvider(Start));

        for (var i = 0; i <= StatsHistoryStore.MaxReplicaSeries; i++)
        {
            store.RecordReplica($"w{i}", new WorkloadTask { Id = "t", Slot = 1 }, new ContainerStatsEntry { TaskId = "t" });
        }

        Assert.IsEmpty(store.ForWorkload($"w{StatsHistoryStore.MaxReplicaSeries}", StatsHistoryRange.ThirtyMinutes));
    }

    [TestMethod]
    public void A_quiet_replica_goes_once_it_has_aged_out()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new StatsHistoryStore(clock);
        store.RecordReplica("w", new WorkloadTask { Id = "a", Slot = 1 }, new ContainerStatsEntry { TaskId = "a" });

        clock.Now = Start.AddHours(25);
        store.RecordMachine(new MachineStats { NodeId = "n", CpuPercent = 1 });

        Assert.IsEmpty(store.ForWorkload("w", StatsHistoryRange.Day));
    }

    [TestMethod]
    public void A_machines_first_read_has_no_cpu_so_it_adds_no_point()
    {
        var store = new StatsHistoryStore(new FakeTimeProvider(Start));

        store.RecordMachine(new MachineStats { NodeId = "n", CpuPercent = null, MemoryTotalBytes = 8 });

        var machine = store.Machines(StatsHistoryRange.ThirtyMinutes).Single();
        Assert.IsEmpty(machine.Points);
        Assert.AreEqual(8, machine.MemoryTotalBytes);
    }
}
