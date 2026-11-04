using FifthBox.ServerManager.Client.Web.Components;
using FifthBox.ServerManager.Client.Web.Services;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class UsageTextTests
{
    private const long Mb = 1024 * 1024;

    [TestMethod]
    public void A_replica_shows_cpu_memory_and_its_limit() =>
        Assert.AreEqual("12.5% CPU · 412 MB / 1 GB", UsageText.Replica(new ContainerStatsEntry
        {
            TaskId = "t",
            CpuPercent = 12.5,
            MemoryBytes = 412 * Mb,
            MemoryLimitBytes = 1024 * Mb,
        }));

    [TestMethod]
    [DataRow(2.0, "180% of 2 cores · 1 GB")]
    [DataRow(1.0, "180% of 1 core · 1 GB")]
    [DataRow(0.5, "180% of 0.5 cores · 1 GB")]
    public void A_cpu_limit_says_how_many_cores_its_percent_is_of(double limit, string expected) =>
        Assert.AreEqual(expected, UsageText.Replica(new ContainerStatsEntry { TaskId = "t", CpuPercent = 180, MemoryBytes = 1024 * Mb }, limit));

    [TestMethod]
    public void A_replica_without_a_limit_leaves_it_off() =>
        Assert.AreEqual("250% CPU · 1.5 GB", UsageText.Replica(new ContainerStatsEntry { TaskId = "t", CpuPercent = 250, MemoryBytes = 1536 * Mb }));

    [TestMethod]
    [DataRow(32.0, "CPU of 32 cores")]
    [DataRow(1.0, "CPU of 1 core")]
    [DataRow(0.5, "CPU of 0.5 cores")]
    [DataRow(null, "CPU")]
    public void The_cpu_title_says_what_the_percent_is_a_share_of(double? cores, string expected) =>
        Assert.AreEqual(expected, UsageText.CpuTitle(cores));

    [TestMethod]
    public void A_value_is_gone_once_it_goes_stale()
    {
        var values = new RecentValues<string>();
        var now = DateTimeOffset.UnixEpoch;
        values.Set("k", "v", now);

        Assert.AreEqual("v", values.Get("k", now + RecentValues<string>.MaxAge));
        Assert.IsNull(values.Get("k", now + RecentValues<string>.MaxAge + TimeSpan.FromSeconds(1)));
    }
}
