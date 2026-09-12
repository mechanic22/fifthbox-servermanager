using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Nodes;
using FifthBox.ServerManager.App.Platform;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Routes;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadSecretTests
{
    private static readonly Caller Admin = new("admin", IsAdmin: true);

    /// Reversible, and a fresh nonce every call — the property that makes re-encrypting an unchanged
    /// secret look like a config change if the service gets it wrong.
    private sealed class ReversibleProtector : ISecretProtector
    {
        private int _nonce;

        public string Protect(string plaintext) => $"enc{Interlocked.Increment(ref _nonce)}:{plaintext}";

        public string Unprotect(string ciphertext) => ciphertext[(ciphertext.IndexOf(':') + 1)..];
    }

    private static (WorkloadService svc, Mock<IWorkloadRepository> repo, Mock<IWorkloadBackend> backend) Build(Workload? existing = null)
    {
        var repo = new Mock<IWorkloadRepository>();
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing is null ? [] : [existing]);
        if (existing is not null)
        {
            repo.Setup(r => r.FindByIdAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        }

        var agents = new Mock<IAgentRepository>();
        agents.Setup(a => a.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Agent>());
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<WorkloadGroup>());
        var backend = new Mock<IWorkloadBackend>();
        var resolver = new Mock<IWorkloadBackendResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<WorkloadKind>())).Returns(backend.Object);
        var nodes = new Mock<INodeService>();
        nodes.Setup(n => n.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<NodeResponse>());
        backend.Setup(b => b.GetStatusAsync(It.IsAny<WorkloadDeployment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkloadRuntimeStatus { Name = "web", Deployed = true });

        var settings = new Mock<IPlatformSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformSettings());
        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        grants.Setup(g => g.RemoveForTargetAsync(It.IsAny<AccessScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var protector = new ReversibleProtector();
        var cluster = Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" });
        var access = new WorkloadAccess(grants.Object, groups.Object, TeamRepo.None());
        var loader = new WorkloadLoader(repo.Object, access);
        var factory = new WorkloadDeploymentFactory(protector, cluster);
        var lifecycle = new WorkloadLifecycleService(
            loader, repo.Object, access, resolver.Object, new Mock<IDeployedSpecSource>().Object,
            new ClusterState(), factory, TimeProvider.System);

        var svc = new WorkloadService(repo.Object, loader, lifecycle, agents.Object, new Mock<IAgentRegistry>().Object,
            groups.Object, access, grants.Object, resolver.Object,
            new Mock<IRouteService>().Object, settings.Object, nodes.Object,
            protector, factory, TimeProvider.System);

        return (svc, repo, backend);
    }

    private static CreateWorkloadRequest WithSecret(string value) => new()
    {
        Name = "web",
        Image = "nginx:1.27",
        Env = [new EnvVar("PLAIN", "yes"), new EnvVar("DB_PASSWORD", value, Secret: true)],
    };

    [TestMethod]
    public async Task Secret_values_are_encrypted_at_rest_and_blanked_in_responses()
    {
        var (svc, repo, _) = Build();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        var response = await svc.CreateAsync(Admin, WithSecret("hunter2"));

        var stored = saved!.Env.Single(v => v.Key == "DB_PASSWORD");
        Assert.AreNotEqual("hunter2", stored.Value, "the plaintext must not reach the database");
        StringAssert.EndsWith(stored.Value, ":hunter2");
        Assert.AreEqual(string.Empty, response.Env.Single(v => v.Key == "DB_PASSWORD").Value);
        Assert.AreEqual("yes", response.Env.Single(v => v.Key == "PLAIN").Value, "plain vars are untouched");
    }

    [TestMethod]
    public async Task A_blank_secret_on_update_keeps_the_stored_value_byte_for_byte()
    {
        var existing = new Workload
        {
            Id = "w1",
            Name = "web",
            Image = "nginx:1.27",
            Kind = WorkloadKind.Container,
            Env = [new EnvVar("DB_PASSWORD", "enc99:hunter2", Secret: true)],
        };
        var (svc, _, _) = Build(existing);

        await svc.UpdateAsync(Admin, "w1", new UpdateWorkloadRequest
        {
            Image = "nginx:1.27",
            Replicas = 1,
            Env = [new EnvVar("DB_PASSWORD", "", Secret: true)],
        });

        Assert.AreEqual("enc99:hunter2", existing.Env.Single().Value);
    }

    [TestMethod]
    public async Task Saving_twice_without_touching_a_secret_leaves_no_pending_changes()
    {
        // The regression this guards: re-encrypting an unchanged secret on every save produces new
        // ciphertext, so the signature drifts from the deployed revision and the UI claims a pending
        // change that the operator never made.
        var (svc, repo, _) = Build();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await svc.CreateAsync(Admin, WithSecret("hunter2"));
        repo.Setup(r => r.FindByIdAsync(saved!.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);

        await svc.DeployAsync(Admin, saved!.Id);

        var afterResave = await svc.UpdateAsync(Admin, saved.Id, new UpdateWorkloadRequest
        {
            Image = "nginx:1.27",
            Replicas = 1,
            Env = [new EnvVar("PLAIN", "yes"), new EnvVar("DB_PASSWORD", "", Secret: true)],
        });

        Assert.IsFalse(afterResave.HasPendingChanges);
    }

    [TestMethod]
    public async Task Changing_a_secret_does_count_as_a_pending_change()
    {
        var (svc, repo, _) = Build();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await svc.CreateAsync(Admin, WithSecret("hunter2"));
        repo.Setup(r => r.FindByIdAsync(saved!.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        await svc.DeployAsync(Admin, saved!.Id);

        var afterChange = await svc.UpdateAsync(Admin, saved.Id, new UpdateWorkloadRequest
        {
            Image = "nginx:1.27",
            Replicas = 1,
            Env = [new EnvVar("PLAIN", "yes"), new EnvVar("DB_PASSWORD", "letmein", Secret: true)],
        });

        Assert.IsTrue(afterChange.HasPendingChanges);
    }

    [TestMethod]
    public async Task The_backend_receives_the_decrypted_value()
    {
        var (svc, repo, backend) = Build();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await svc.CreateAsync(Admin, WithSecret("hunter2"));
        repo.Setup(r => r.FindByIdAsync(saved!.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);

        await svc.DeployAsync(Admin, saved!.Id);

        backend.Verify(b => b.DeployAsync(
            It.Is<WorkloadDeployment>(d => d.Env.Single(v => v.Key == "DB_PASSWORD").Value == "hunter2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task A_brand_new_secret_with_no_value_is_rejected()
    {
        var (svc, _, _) = Build();

        await Assert.ThrowsExactlyAsync<ValidationException>(() => svc.CreateAsync(Admin, WithSecret("")));
    }

    [TestMethod]
    public async Task Revision_history_never_returns_secret_values()
    {
        var (svc, repo, _) = Build();
        Workload? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Workload>(), It.IsAny<CancellationToken>()))
            .Callback<Workload, CancellationToken>((w, _) => saved = w).Returns(Task.CompletedTask);

        await svc.CreateAsync(Admin, WithSecret("hunter2"));
        repo.Setup(r => r.FindByIdAsync(saved!.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        await svc.DeployAsync(Admin, saved!.Id);

        var revisions = await svc.GetRevisionsAsync(Admin, saved.Id);

        Assert.AreEqual(string.Empty, revisions.Single().Env.Single(v => v.Key == "DB_PASSWORD").Value);
    }
}
