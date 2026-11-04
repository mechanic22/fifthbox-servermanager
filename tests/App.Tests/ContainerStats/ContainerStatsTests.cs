using System.Text;
using System.Text.Json;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests.ContainerStats;

[TestClass]
public class ContainerStatsTests
{
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private const string Key = "test-signing-key";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly ContainerStatsStore _store;
    private readonly StatsHistoryStore _history;
    private readonly Mock<IContainerStatsNotifier> _notifier = new();
    private readonly ContainerStatsIngest _ingest;

    public ContainerStatsTests()
    {
        _store = new ContainerStatsStore(_clock);
        _history = new StatsHistoryStore(_clock);
        _ingest = Ingest(Key);
    }

    private ContainerStatsIngest Ingest(string? key) =>
        new(_store, _history, new ClusterState(), _notifier.Object, Options.Create(new ContainerStatsOptions { SigningKey = key }), _clock);

    private static ContainerStatsEntry Entry(string taskId = "task1", double cpu = 12.5, long memory = 1024, long? limit = 2048) =>
        new() { TaskId = taskId, CpuPercent = cpu, MemoryBytes = memory, MemoryLimitBytes = limit };

    private static byte[] Body(params ContainerStatsEntry[] entries) =>
        JsonSerializer.SerializeToUtf8Bytes(new ContainerStatsReport { Containers = entries }, Web);

    private long Now => _clock.GetUtcNow().ToUnixTimeSeconds();

    private Task Send(byte[] body, string key = Key) =>
        _ingest.IngestAsync(body, Now.ToString(), ContainerStatsSignature.Sign(key, Now, body));

    [TestMethod]
    public async Task A_signed_report_is_stored()
    {
        await Send(Body(Entry()));

        Assert.AreEqual(12.5, _store.Find("task1")!.CpuPercent);
    }

    [TestMethod]
    public async Task A_wrong_key_is_rejected_and_nothing_stored()
    {
        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => Send(Body(Entry()), "other-key"));
        Assert.IsNull(_store.Find("task1"));
    }

    [TestMethod]
    public async Task A_changed_body_is_rejected()
    {
        var signature = ContainerStatsSignature.Sign(Key, Now, Body(Entry(cpu: 1)));

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => _ingest.IngestAsync(Body(Entry(cpu: 99)), Now.ToString(), signature));
    }

    [TestMethod]
    public async Task A_changed_timestamp_is_rejected()
    {
        var body = Body(Entry());
        var signature = ContainerStatsSignature.Sign(Key, Now, body);

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => _ingest.IngestAsync(body, (Now + 1).ToString(), signature));
    }

    [TestMethod]
    [DataRow(-61)]
    [DataRow(61)]
    public async Task Reports_outside_the_window_are_rejected(int offsetSeconds)
    {
        var body = Body(Entry());
        var at = Now + offsetSeconds;

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => _ingest.IngestAsync(body, at.ToString(), ContainerStatsSignature.Sign(Key, at, body)));
    }

    [TestMethod]
    [DataRow(null, "sig")]
    [DataRow("", "sig")]
    [DataRow("not-a-number", "sig")]
    [DataRow("-1", "sig")]
    [DataRow("99999999999999999", "sig")]
    [DataRow("1793793600", null)]
    [DataRow("1793793600", "")]
    public Task Missing_or_garbage_headers_are_rejected(string? timestamp, string? signature) =>
        Assert.ThrowsExactlyAsync<UnauthorizedException>(() => _ingest.IngestAsync(Body(Entry()), timestamp, signature));

    [TestMethod]
    public async Task Everything_is_rejected_without_a_signing_key()
    {
        var body = Body(Entry());

        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => Ingest(null).IngestAsync(body, Now.ToString(), ContainerStatsSignature.Sign(string.Empty, Now, body)));
    }

    [TestMethod]
    public Task An_unsigned_bad_body_fails_on_the_signature_not_the_parser() =>
        Assert.ThrowsExactlyAsync<UnauthorizedException>(() => _ingest.IngestAsync(Encoding.UTF8.GetBytes("{not json"), Now.ToString(), "bad"));

    [TestMethod]
    public Task A_signed_bad_body_is_a_validation_error() =>
        Assert.ThrowsExactlyAsync<ValidationException>(() => Send(Encoding.UTF8.GetBytes("{not json")));

    [TestMethod]
    public async Task Bad_entries_are_skipped_and_good_ones_kept()
    {
        await Send(Body(
            Entry("good"),
            Entry("../../etc"),
            Entry(new string('a', 65)),
            Entry("negcpu", cpu: -1),
            Entry("negmem", memory: -1),
            Entry("zerolimit", limit: 0)));

        Assert.IsNotNull(_store.Find("good"));
        Assert.AreEqual(1, _store.Count);
    }

    [TestMethod]
    public async Task A_report_is_cut_off_at_the_entry_cap()
    {
        await Send(Body(Enumerable.Range(0, ContainerStatsIngest.MaxEntriesPerReport + 5).Select(i => Entry($"t{i}")).ToArray()));

        Assert.AreEqual(ContainerStatsIngest.MaxEntriesPerReport, _store.Count);
    }

    [TestMethod]
    public void Numbers_expire_when_a_node_stops_reporting()
    {
        _store.Record([Entry()]);

        _clock.Advance(ContainerStatsStore.Ttl + TimeSpan.FromSeconds(1));

        Assert.IsNull(_store.Find("task1"));
    }

    [TestMethod]
    public void Expired_entries_are_dropped_on_the_next_write()
    {
        _store.Record([Entry("old")]);
        _clock.Advance(ContainerStatsStore.Ttl + TimeSpan.FromSeconds(1));

        _store.Record([Entry("new")]);

        Assert.AreEqual(1, _store.Count);
    }

    [TestMethod]
    public void The_store_stops_taking_new_ids_at_its_cap_but_still_updates_known_ones()
    {
        _store.Record(Enumerable.Range(0, ContainerStatsStore.MaxEntries).Select(i => Entry($"t{i}")));

        _store.Record([Entry("overflow"), Entry("t0", cpu: 50)]);

        Assert.IsNull(_store.Find("overflow"));
        Assert.AreEqual(50, _store.Find("t0")!.CpuPercent);
    }

    private static MachineStats Machine(string node = "node1", double? cpu = 12, long memory = 100, long? diskFree = 5) =>
        new() { NodeId = node, CpuPercent = cpu, MemoryUsedBytes = memory, MemoryTotalBytes = 200, DiskFreeBytes = diskFree, DiskTotalBytes = 10 };

    private Task SendMachine(MachineStats machine) =>
        Send(JsonSerializer.SerializeToUtf8Bytes(new ContainerStatsReport { Machine = machine }, Web));

    [TestMethod]
    public async Task A_machine_report_is_stored()
    {
        await SendMachine(Machine());

        var machine = _history.Machines(StatsHistoryRange.ThirtyMinutes).Single();
        Assert.AreEqual("node1", machine.NodeId);
        Assert.AreEqual(5, machine.DiskFreeBytes);
    }

    [TestMethod]
    [DataRow("../x", 12.0, 100L, 5L)]
    [DataRow("node1", 101.0, 100L, 5L)]
    [DataRow("node1", -1.0, 100L, 5L)]
    [DataRow("node1", 12.0, -1L, 5L)]
    [DataRow("node1", 12.0, 100L, -5L)]
    public async Task A_bad_machine_report_is_skipped(string node, double cpu, long memory, long diskFree)
    {
        await SendMachine(Machine(node, cpu, memory, diskFree));

        Assert.IsEmpty(_history.Machines(StatsHistoryRange.ThirtyMinutes));
    }

    [TestMethod]
    public async Task Viewers_are_only_told_what_passed_the_checks()
    {
        await Send(Body(Entry("good"), Entry("../bad")));

        _notifier.Verify(n => n.NotifyAsync(It.Is<ContainerStatsReport>(r => r.Containers.Single().TaskId == "good"), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task A_rejected_report_tells_nobody()
    {
        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => Send(Body(Entry()), "other-key"));

        _notifier.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void Stats_only_go_to_the_workload_that_owns_the_task()
    {
        var statuses = new Dictionary<string, WorkloadRuntimeStatus>
        {
            ["w1"] = new() { Name = "a", Tasks = [new WorkloadTask { Id = "t1" }] },
            ["w2"] = new() { Name = "b", Tasks = [new WorkloadTask { Id = "t2" }] },
        };

        var routed = ContainerStatsRouting.ByWorkload(statuses, [Entry("t1"), Entry("madeup")]);

        Assert.AreEqual("t1", routed["w1"].Single().TaskId);
        Assert.HasCount(1, routed);
    }

    // same known value as StatsTests on the worker side
    [TestMethod]
    public void The_signature_matches_the_known_value_the_worker_sends() =>
        Assert.AreEqual(
            "ae87545d86a3549cecec646cc067306f9764989f695547ec78ce42ff5f25d115",
            ContainerStatsSignature.Sign("test-signing-key", 1793793600, Encoding.UTF8.GetBytes("""{"containers":[]}""")));

    [TestMethod]
    public void The_derived_key_is_stable_and_is_not_the_encryption_key()
    {
        var key = ContainerStatsSignature.DeriveKey("encryption-key");

        Assert.AreEqual(key, ContainerStatsSignature.DeriveKey("encryption-key"));
        Assert.AreNotEqual(key, ContainerStatsSignature.DeriveKey("different-key"));
        Assert.DoesNotContain("encryption-key", key);
    }
}
