using FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class ResourceScaleTests
{
    [TestMethod]
    public void Memory_reads_as_MB_until_a_gigabyte_and_then_as_GB()
    {
        Assert.AreEqual("No limit", ResourceScale.MemoryLabel(0));
        Assert.AreEqual("512 MB", ResourceScale.MemoryLabel(512));
        Assert.AreEqual("1 GB", ResourceScale.MemoryLabel(1024));
        Assert.AreEqual("1.5 GB", ResourceScale.MemoryLabel(1536));
        Assert.AreEqual("64 GB", ResourceScale.MemoryLabel(65536));
    }

    [TestMethod]
    public void Cpu_counts_cores_and_knows_when_there_is_one()
    {
        Assert.AreEqual("No limit", ResourceScale.CpuLabel(0));
        Assert.AreEqual("0.5 cores", ResourceScale.CpuLabel(0.5));
        Assert.AreEqual("1 core", ResourceScale.CpuLabel(1));
        Assert.AreEqual("4 cores", ResourceScale.CpuLabel(4));
    }

    [TestMethod]
    public void A_reservation_is_only_offered_what_its_limit_allows()
    {
        var options = ResourceScale.MemoryOptions(current: 0, ceiling: 1024);

        Assert.AreEqual(1024, options.Max());
        Assert.Contains(0, options, "no limit stays available whatever the ceiling");
    }

    [TestMethod]
    public void No_ceiling_offers_the_whole_scale()
    {
        Assert.HasCount(ResourceScale.MemoryMb.Length, ResourceScale.MemoryOptions(current: 0));
        Assert.HasCount(ResourceScale.CpuCores.Length, ResourceScale.CpuOptions(current: 0));
    }

    [TestMethod]
    public void A_value_saved_before_the_scale_existed_keeps_its_place()
    {
        // Rounding it to the nearest stop would change the workload just by opening its Config tab.
        var options = ResourceScale.MemoryOptions(current: 384);

        Assert.Contains(384, options);
        CollectionAssert.AreEqual(options.Order().ToList(), options, "the odd value is sorted in, not appended");
    }

    [TestMethod]
    public void An_off_scale_cpu_value_survives_its_ceiling()
    {
        var options = ResourceScale.CpuOptions(current: 1.3, ceiling: 2);

        Assert.Contains(1.3, options);
        Assert.AreEqual(2, options.Max());
    }
}
