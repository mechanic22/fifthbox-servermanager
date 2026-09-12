using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadBackendResolverTests
{
    private static Mock<IWorkloadBackend> Backend(WorkloadKind kind)
    {
        var mock = new Mock<IWorkloadBackend>();
        mock.SetupGet(b => b.SupportedKind).Returns(kind);
        return mock;
    }

    [TestMethod]
    public void Resolve_returns_the_backend_matching_the_kind()
    {
        var container = Backend(WorkloadKind.Container);
        var native = Backend(WorkloadKind.Native);
        var resolver = new WorkloadBackendResolver([container.Object, native.Object]);

        Assert.AreSame(native.Object, resolver.Resolve(WorkloadKind.Native));
        Assert.AreSame(container.Object, resolver.Resolve(WorkloadKind.Container));
    }

    [TestMethod]
    public void Resolve_throws_when_no_backend_supports_the_kind()
    {
        var resolver = new WorkloadBackendResolver([Backend(WorkloadKind.Container).Object]);

        Assert.ThrowsExactly<InvalidOperationException>(() => resolver.Resolve(WorkloadKind.Native));
    }
}
