using System.Text;
using System.Text.Json;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FifthBox.ServerManager.Stats.Tests;

[TestClass]
public class StatsTests
{
    private static DockerStatsSample Sample(
        ulong cpu = 200, ulong previousCpu = 100, ulong system = 2000, ulong previousSystem = 1000,
        int cores = 4, long usage = 1000, long limit = 0, Dictionary<string, long>? memoryStats = null) => new()
    {
        Cpu = new CpuSample { Usage = new CpuUsage { Total = cpu }, SystemUsage = system, OnlineCpus = cores },
        PreviousCpu = new CpuSample { Usage = new CpuUsage { Total = previousCpu }, SystemUsage = previousSystem },
        Memory = new MemorySample { Usage = usage, Limit = limit, Stats = memoryStats },
    };

    [TestMethod]
    public void Cpu_matches_docker_stats_where_100_is_one_core() =>
        Assert.AreEqual(40, StatsCalculator.CpuPercent(Sample()));

    [TestMethod]
    public void Cpu_can_go_past_100_on_several_cores() =>
        Assert.AreEqual(250, StatsCalculator.CpuPercent(Sample(cpu: 725, previousCpu: 100, cores: 4)));

    [TestMethod]
    public void Cpu_is_zero_when_counters_went_backwards_after_a_restart() =>
        Assert.AreEqual(0, StatsCalculator.CpuPercent(Sample(cpu: 50, previousCpu: 100)));

    [TestMethod]
    public void Cpu_is_zero_without_a_previous_sample() =>
        Assert.AreEqual(0, StatsCalculator.CpuPercent(new DockerStatsSample { Cpu = new CpuSample() }));

    [TestMethod]
    public void Cpu_falls_back_to_counting_cores_when_online_cpus_is_missing()
    {
        var sample = new DockerStatsSample
        {
            Cpu = new CpuSample { Usage = new CpuUsage { Total = 200, PerCpu = [1, 2] }, SystemUsage = 2000 },
            PreviousCpu = new CpuSample { Usage = new CpuUsage { Total = 100 }, SystemUsage = 1000 },
        };

        Assert.AreEqual(20, StatsCalculator.CpuPercent(sample));
    }

    [TestMethod]
    [DataRow("inactive_file")]
    [DataRow("total_inactive_file")]
    public void Memory_leaves_out_page_cache_on_both_cgroup_versions(string key) =>
        Assert.AreEqual(700, StatsCalculator.MemoryUsed(new MemorySample { Usage = 1000, Stats = new() { [key] = 300 } }));

    [TestMethod]
    public void Memory_never_goes_negative() =>
        Assert.AreEqual(0, StatsCalculator.MemoryUsed(new MemorySample { Usage = 100, Stats = new() { ["inactive_file"] = 300 } }));

    [TestMethod]
    public void A_limit_below_the_machines_memory_is_a_real_limit() =>
        Assert.AreEqual(512L, StatsCalculator.ToEntry("t", Sample(limit: 512), 4096).MemoryLimitBytes);

    [TestMethod]
    public void A_limit_equal_to_the_machines_memory_means_no_limit() =>
        Assert.IsNull(StatsCalculator.ToEntry("t", Sample(limit: 4096), 4096).MemoryLimitBytes);

    [TestMethod]
    public void No_limit_is_reported_when_the_machines_memory_is_unknown() =>
        Assert.IsNull(StatsCalculator.ToEntry("t", Sample(limit: 512), null).MemoryLimitBytes);

    [TestMethod]
    public void MemTotal_is_read_from_meminfo() =>
        Assert.AreEqual(2048L * 1024, StatsCalculator.MemInfoBytes("MemFree: 1 kB\nMemTotal:     2048 kB\nSwapTotal: 0 kB\n", "MemTotal"));

    [TestMethod]
    [DataRow("")]
    [DataRow("MemFree: 1 kB")]
    [DataRow("MemTotal: nonsense kB")]
    public void Unreadable_meminfo_is_null(string meminfo) =>
        Assert.IsNull(StatsCalculator.MemInfoBytes(meminfo, "MemTotal"));

    [TestMethod]
    public void Cpu_ticks_count_iowait_as_idle()
    {
        var ticks = StatsCalculator.ParseCpuTicks("cpu  10 20 30 40 50 60 70 80 90 100\ncpu0 1 2 3 4\n");

        Assert.AreEqual(new CpuTicks(360, 90), ticks);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("intr 1 2 3")]
    [DataRow("cpu  a b c d e f")]
    public void Unreadable_stat_is_null(string stat) =>
        Assert.IsNull(StatsCalculator.ParseCpuTicks(stat));

    [TestMethod]
    public void Cores_are_counted_from_the_per_cpu_lines() =>
        Assert.AreEqual(2, StatsCalculator.CountCores("cpu  1 2 3 4\ncpu0 1 2 3 4\ncpu1 1 2 3 4\nintr 5\ncpufreq 1\n"));

    [TestMethod]
    public void No_per_cpu_lines_is_unknown_cores() =>
        Assert.IsNull(StatsCalculator.CountCores("cpu  1 2 3 4\nintr 5\n"));

    [TestMethod]
    public void Machine_cpu_is_the_busy_share_of_the_delta() =>
        Assert.AreEqual(25, StatsCalculator.MachineCpuPercent(new CpuTicks(1000, 800), new CpuTicks(1100, 875)));

    [TestMethod]
    public void Machine_cpu_is_zero_when_the_counters_didnt_move() =>
        Assert.AreEqual(0, StatsCalculator.MachineCpuPercent(new CpuTicks(1000, 800), new CpuTicks(1000, 800)));

    [TestMethod]
    public void Machine_cpu_never_leaves_0_to_100() =>
        Assert.AreEqual(0, StatsCalculator.MachineCpuPercent(new CpuTicks(1000, 800), new CpuTicks(1100, 950)));

    // same known value as ContainerStatsTests on the Host side
    [TestMethod]
    public void The_signature_matches_the_known_value_the_host_checks() =>
        Assert.AreEqual(
            "ae87545d86a3549cecec646cc067306f9764989f695547ec78ce42ff5f25d115",
            ReportEnvelope.Sign("test-signing-key", "1793793600", Encoding.UTF8.GetBytes("""{"containers":[]}""")));

    [TestMethod]
    public void The_envelope_signs_exactly_the_body_it_carries()
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        var envelope = ReportEnvelope.Build(new ContainerStatsReport(), "key", now);

        Assert.AreEqual(now.ToUnixTimeSeconds().ToString(), envelope.Timestamp);
        Assert.AreEqual(ReportEnvelope.Sign("key", envelope.Timestamp, envelope.Body), envelope.Signature);
    }

    [TestMethod]
    public void A_report_carries_only_the_task_id_and_numbers()
    {
        var report = new ContainerStatsReport { Containers = [StatsCalculator.ToEntry("task1", Sample(limit: 512), 4096)] };

        var body = ReportEnvelope.Build(report, "key", DateTimeOffset.UnixEpoch).Body;

        using var json = JsonDocument.Parse(body);
        var fields = json.RootElement.GetProperty("containers")[0].EnumerateObject().Select(p => p.Name).Order().ToArray();
        CollectionAssert.AreEqual(new[] { "cpuPercent", "memoryBytes", "memoryLimitBytes", "taskId" }, fields);
    }

    [TestMethod]
    public async Task A_container_that_vanishes_mid_read_is_skipped_and_the_rest_still_report()
    {
        var docker = new Mock<IDockerStats>();
        docker.Setup(d => d.ListSwarmContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SwarmContainer("a", "taskA"), new SwarmContainer("b", "taskB")]);
        docker.Setup(d => d.ReadStatsAsync("a", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gone"));
        docker.Setup(d => d.ReadStatsAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sample());

        var report = await new StatsCollector(docker.Object, NullLogger<StatsCollector>.Instance).CollectAsync(4096, CancellationToken.None);

        Assert.HasCount(1, report.Containers);
        Assert.AreEqual("taskB", report.Containers[0].TaskId);
    }
}
