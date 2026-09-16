using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Teams;
using Moq;

using RoleNames = FifthBox.ServerManager.Shared.Auth.Roles;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class TeamServiceTests
{
    private static readonly IReadOnlyList<string> Admin = [RoleNames.Admin];
    private static readonly IReadOnlyList<string> NoRoles = [];
    private static readonly DirectoryUser Friend = new("u1", "friend", NoRoles);
    private static readonly DirectoryUser Client = new("u2", "client", NoRoles);
    private static readonly DirectoryUser Boss = new("u3", "boss", Admin);

    private static readonly Caller Owner = new("u3", IsAdmin: true);
    private static readonly Caller Nobody = new("u1", IsAdmin: false);

    private static (TeamService Svc, Mock<ITeamRepository> Teams, Mock<IAccessGrantRepository> Grants) Build(
        params Team[] existing)
    {
        var stored = existing.ToList();

        var teams = new Mock<ITeamRepository>();
        teams.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => [.. stored]);
        teams.Setup(t => t.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => stored.FirstOrDefault(t => t.Id == id));
        teams.Setup(t => t.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Callback((Team t, CancellationToken _) => stored.Add(t)).Returns(Task.CompletedTask);
        teams.Setup(t => t.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        teams.Setup(t => t.RemoveAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Callback((Team t, CancellationToken _) => stored.Remove(t)).Returns(Task.CompletedTask);

        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Friend, Client, Boss]);

        var grants = new Mock<IAccessGrantRepository>();
        grants.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        grants.Setup(g => g.RemoveForSubjectAsync(It.IsAny<GrantSubject>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new TeamService(teams.Object, users.Object, grants.Object, TimeProvider.System), teams, grants);
    }

    [TestMethod]
    public async Task Create_trims_the_name()
    {
        var (svc, _, _) = Build();
        var team = await svc.CreateAsync(Owner, new SaveTeamRequest { Name = "  Ops  " });
        Assert.AreEqual("Ops", team.Name);
    }

    [TestMethod]
    public async Task A_blank_name_is_rejected()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => svc.CreateAsync(Owner, new SaveTeamRequest { Name = "   " }));
    }

    [TestMethod]
    public async Task A_duplicate_name_is_rejected_whatever_its_casing()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });
        await Assert.ThrowsExactlyAsync<ConflictException>(
            () => svc.CreateAsync(Owner, new SaveTeamRequest { Name = "ops" }));
    }

    [TestMethod]
    public async Task Renaming_a_team_to_its_own_name_is_allowed()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });
        var team = await svc.UpdateAsync(Owner, "t1", new SaveTeamRequest { Name = "Ops", Description = "on call" });
        Assert.AreEqual("on call", team.Description);
    }

    [TestMethod]
    public async Task Members_are_resolved_to_names_and_admin_membership_is_flagged()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });
        var team = await svc.SetMembersAsync(Owner, "t1", new SetTeamMembersRequest { UserIds = ["u1", "u3"] });

        CollectionAssert.AreEqual(new[] { "boss", "friend" }, team.Members.Select(m => m.UserName).ToArray());
        Assert.IsTrue(team.Members.Single(m => m.UserName == "boss").IsAdmin);
        Assert.IsFalse(team.Members.Single(m => m.UserName == "friend").IsAdmin);
    }

    [TestMethod]
    public async Task Duplicate_member_ids_collapse()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });
        var team = await svc.SetMembersAsync(Owner, "t1", new SetTeamMembersRequest { UserIds = ["u1", "u1", "  "] });
        Assert.HasCount(1, team.Members);
    }

    [TestMethod]
    public async Task An_unknown_member_is_rejected()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });
        var error = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => svc.SetMembersAsync(Owner, "t1", new SetTeamMembersRequest { UserIds = ["u1", "ghost"] }));

        StringAssert.Contains(error.Errors[nameof(SetTeamMembersRequest.UserIds)].Single(), "ghost");
    }

    [TestMethod]
    public async Task Deleting_a_team_deletes_the_grants_it_held()
    {
        var (svc, _, grants) = Build(new Team { Id = "t1", Name = "Ops" });
        await svc.DeleteAsync(Owner, "t1");

        grants.Verify(g => g.RemoveForSubjectAsync(GrantSubject.Team("t1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task An_unknown_team_is_a_404_not_a_silent_no_op()
    {
        var (svc, _, _) = Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() => svc.DeleteAsync(Owner, "t1"));
    }

    [TestMethod]
    public async Task Only_an_administrator_may_manage_teams()
    {
        var (svc, _, _) = Build(new Team { Id = "t1", Name = "Ops" });

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.ListAsync(Nobody));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.CreateAsync(Nobody, new SaveTeamRequest { Name = "x" }));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.UpdateAsync(Nobody, "t1", new SaveTeamRequest { Name = "x" }));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.SetMembersAsync(Nobody, "t1", new SetTeamMembersRequest()));
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => svc.DeleteAsync(Nobody, "t1"));
    }

    [TestMethod]
    public async Task The_list_counts_only_this_teams_grants()
    {
        var (svc, _, grants) = Build(new Team { Id = "t1", Name = "Ops" }, new Team { Id = "t2", Name = "Clients" });
        grants.Setup(g => g.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w1", Level = AccessLevel.View },
            new AccessGrant { SubjectType = AccessSubject.Team, SubjectId = "t1", Scope = AccessScope.Group, TargetId = "g1", Level = AccessLevel.Operate },
            // user grant with the same id must not count as the team's
            new AccessGrant { SubjectType = AccessSubject.User, SubjectId = "t1", Scope = AccessScope.Workload, TargetId = "w2", Level = AccessLevel.View },
        ]);

        var listed = await svc.ListAsync(Owner);
        Assert.AreEqual(2, listed.Single(t => t.Id == "t1").GrantCount);
        Assert.AreEqual(0, listed.Single(t => t.Id == "t2").GrantCount);
    }
}
