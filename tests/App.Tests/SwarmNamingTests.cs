using FifthBox.ServerManager.App.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class SwarmNamingTests
{
    [TestMethod]
    public void A_workload_service_is_namespaced()
        => Assert.AreEqual("fbsm--grafana", SwarmNaming.ServiceName("grafana"));

    [TestMethod]
    public void The_namespace_cannot_collide_with_a_platform_service()
    {
        // a single dash would give fbsm-host and fbsm-nginx, and deploy would update those instead
        Assert.AreNotEqual("fbsm-host", SwarmNaming.ServiceName("host"));
        Assert.AreNotEqual("fbsm-nginx", SwarmNaming.ServiceName("nginx"));
    }

    [TestMethod]
    public void A_namespaced_service_reads_back_to_its_workload()
    {
        Assert.IsTrue(SwarmNaming.TryWorkloadName(SwarmNaming.ServiceName("grafana"), out var name));
        Assert.AreEqual("grafana", name);
    }

    [TestMethod]
    public void Reading_back_rejects_anything_that_is_not_ours()
    {
        Assert.IsFalse(SwarmNaming.TryWorkloadName("fbsm-nginx", out _));
        Assert.IsFalse(SwarmNaming.TryWorkloadName("fbsm-host", out _));
        Assert.IsFalse(SwarmNaming.TryWorkloadName("grafana", out _));
        Assert.IsFalse(SwarmNaming.TryWorkloadName("fbsm--", out _));
        Assert.IsFalse(SwarmNaming.TryWorkloadName(null, out _));
    }

    [TestMethod]
    public void Volumes_are_scoped_to_the_service_that_mounts_them()
    {
        var first = SwarmNaming.VolumeName(SwarmNaming.ServiceName("grafana"), "data");
        var second = SwarmNaming.VolumeName(SwarmNaming.ServiceName("prometheus"), "data");

        Assert.AreEqual("fbsm--grafana--data", first);
        Assert.AreNotEqual(first, second, "two workloads asking for 'data' must not share one volume");
    }
}
