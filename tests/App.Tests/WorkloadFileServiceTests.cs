using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class WorkloadFileServiceTests
{
    private static readonly Caller Admin = new("u1", true);

    private static (WorkloadFileService Service, Mock<IAgentCommandChannel> Agents) Build(Workload workload, bool online = true)
    {
        var repo = new Mock<IWorkloadRepository>();
        repo.Setup(r => r.FindByIdAsync(workload.Id, It.IsAny<CancellationToken>())).ReturnsAsync(workload);

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListForSubjectsAsync(It.IsAny<IReadOnlyList<GrantSubject>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var groups = new Mock<IWorkloadGroupRepository>();
        groups.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var agents = new Mock<IAgentCommandChannel>();
        agents.Setup(a => a.IsConnected(It.IsAny<string>())).Returns(online);

        var loader = new WorkloadLoader(repo.Object, new WorkloadAccess(grants.Object, groups.Object, TeamRepo.None()));
        return (new WorkloadFileService(loader, agents.Object), agents);
    }

    private static Workload Managed() => new()
    {
        Id = "w1", Name = "srcds", Kind = WorkloadKind.Native, AgentId = "a1",
        Command = "srcds", ManagedDirectory = true,
    };

    [TestMethod]
    public async Task Listing_asks_the_agent_for_the_workloads_own_directory()
    {
        var (service, agents) = Build(Managed());
        agents.Setup(a => a.ListFilesAsync("a1", "srcds", "cfg", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await service.ListAsync(Admin, "w1", "cfg");

        agents.Verify(a => a.ListFilesAsync("a1", "srcds", "cfg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task A_container_workload_has_no_files_here()
    {
        // Its files live in a volume the manager can't reach from here — a different feature, not this one.
        var (service, _) = Build(new Workload { Id = "w1", Name = "web", Kind = WorkloadKind.Container, Image = "nginx" });

        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.ListAsync(Admin, "w1", null));
    }

    [TestMethod]
    public async Task A_hand_installed_directory_is_not_ours_to_browse()
    {
        var workload = Managed();
        workload.ManagedDirectory = false;
        var (service, _) = Build(workload);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.ListAsync(Admin, "w1", null));
    }

    [TestMethod]
    public async Task An_offline_agent_is_a_conflict_rather_than_an_empty_listing()
    {
        var (service, _) = Build(Managed(), online: false);

        await Assert.ThrowsExactlyAsync<ConflictException>(() => service.ListAsync(Admin, "w1", null));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Reading_without_naming_a_file_is_refused(string? path)
    {
        var (service, agents) = Build(Managed());

        await Assert.ThrowsExactlyAsync<ValidationException>(() => service.ReadAsync(Admin, "w1", path!));
        agents.Verify(a => a.ReadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Writing_forwards_the_text_to_the_agent()
    {
        var (service, agents) = Build(Managed());

        await service.WriteAsync(Admin, "w1", "cfg/server.cfg", "hostname \"box\"");

        agents.Verify(a => a.WriteFileAsync("a1", "srcds", "cfg/server.cfg", "hostname \"box\"", It.IsAny<CancellationToken>()), Times.Once);
    }
}
