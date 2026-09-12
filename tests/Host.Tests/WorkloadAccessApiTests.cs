using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Teams;
using FifthBox.ServerManager.Shared.Users;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Host.Tests;

/// <summary>
/// Access control over real HTTP: cookie sign-in, the policy on the endpoint group, the App layer's
/// level check, and the exception handler turning the result into a status code. The unit tests prove
/// the rules; these prove the rules are actually wired to the wire.
/// </summary>
[TestClass]
public class WorkloadAccessApiTests
{
    private static AppFactory _factory = null!;
    private static HttpClient _admin = null!;

    [ClassInitialize]
    public static async Task Init(TestContext _)
    {
        _factory = new AppFactory();
        _admin = await _factory.AdminClientAsync();
    }

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [TestMethod]
    public async Task A_workload_you_hold_nothing_on_is_a_404_not_a_403()
    {
        // 403 would confirm the id exists to someone walking the id space.
        var workload = await CreateWorkloadAsync("nothing-shared");
        var friend = await CreateUserAndSignInAsync("outsider");

        using var response = await friend.GetAsync($"/api/workloads/{workload.Id}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task View_can_read_it_but_an_operate_action_is_a_403_with_a_readable_message()
    {
        var workload = await CreateWorkloadAsync("view-only");
        var user = await CreateUserAsync("viewer");
        await GrantAsync(AccessSubject.User, user.UserId, AccessScope.Workload, workload.Id, AccessLevel.View);
        var client = await _factory.SignInAsync(user.UserName, Password);

        using var read = await client.GetAsync($"/api/workloads/{workload.Id}");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);

        using var restart = await client.PostAsync($"/api/workloads/{workload.Id}/restart", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, restart.StatusCode);

        // ForbiddenException had never been thrown in anger; this is the path the snackbar reads.
        var problem = await restart.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.IsNotNull(problem);
        StringAssert.Contains(problem.Detail ?? string.Empty, "permission");
    }

    [TestMethod]
    public async Task The_list_shows_only_what_is_shared_with_you()
    {
        var mine = await CreateWorkloadAsync("listed-mine");
        await CreateWorkloadAsync("listed-someone-elses");
        var user = await CreateUserAsync("lister");
        await GrantAsync(AccessSubject.User, user.UserId, AccessScope.Workload, mine.Id, AccessLevel.View);
        var client = await _factory.SignInAsync(user.UserName, Password);

        var listed = await client.GetFromJsonAsync<List<WorkloadResponse>>("/api/workloads");

        Assert.IsNotNull(listed);
        CollectionAssert.AreEqual(new[] { mine.Id }, listed.Select(w => w.Id).ToArray());
    }

    [TestMethod]
    public async Task A_team_grant_on_a_group_reaches_a_member_through_the_whole_stack()
    {
        var group = await CreateGroupAsync("team-reachable");
        var workload = await CreateWorkloadAsync("in-the-group", group.Id);
        var user = await CreateUserAsync("team-member");

        var team = await PostAsync<TeamResponse>("/api/teams", new SaveTeamRequest { Name = $"ops-{Guid.NewGuid():n}" });
        await PutAsync($"/api/teams/{team.Id}/members", new SetTeamMembersRequest { UserIds = [user.UserId] });
        await GrantAsync(AccessSubject.Team, team.Id, AccessScope.Group, group.Id, AccessLevel.Operate);

        var client = await _factory.SignInAsync(user.UserName, Password);
        var fetched = await client.GetFromJsonAsync<WorkloadResponse>($"/api/workloads/{workload.Id}");

        Assert.IsNotNull(fetched);
        Assert.AreEqual(AccessLevel.Operate, fetched.Access);
    }

    [TestMethod]
    public async Task The_platform_surface_refuses_a_non_admin()
    {
        var client = await CreateUserAndSignInAsync("not-an-admin");

        foreach (var route in new[] { "/api/nodes", "/api/cluster", "/api/routes", "/api/teams", "/api/access/grants" })
        {
            using var response = await client.GetAsync(route);
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, $"{route} let a non-admin in");
        }
    }

    [TestMethod]
    public async Task Creating_a_workload_stays_admin_only_however_much_access_you_hold()
    {
        var workload = await CreateWorkloadAsync("configure-cannot-create");
        var user = await CreateUserAsync("configurer");
        await GrantAsync(AccessSubject.User, user.UserId, AccessScope.Workload, workload.Id, AccessLevel.Configure);
        var client = await _factory.SignInAsync(user.UserName, Password);

        using var response = await client.PostAsJsonAsync(
            "/api/workloads", new CreateWorkloadRequest { Name = "sneaky", Target = WorkloadTarget.Swarm, Image = "nginx" });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Signed_out_means_401_not_a_redirect_to_a_page_the_api_has_no_business_serving()
    {
        using var response = await _factory.CreateClient().GetAsync("/api/workloads");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private const string Password = "test-user-password";

    private sealed record ProblemBody(string? Title, string? Detail);

    private static async Task<WorkloadResponse> CreateWorkloadAsync(string name, string? groupId = null)
        => await PostAsync<WorkloadResponse>("/api/workloads", new CreateWorkloadRequest
        {
            Name = $"{name}-{Guid.NewGuid():n}"[..24],
            GroupId = groupId,
            Target = WorkloadTarget.Swarm,
            Image = "nginx:1.27",
            Replicas = 1,
        });

    private static async Task<WorkloadGroupResponse> CreateGroupAsync(string name)
        => await PostAsync<WorkloadGroupResponse>("/api/workload-groups",
            new CreateWorkloadGroupRequest { Name = $"{name}-{Guid.NewGuid():n}"[..24] });

    private static async Task<UserResponse> CreateUserAsync(string name)
    {
        var email = $"{name}-{Guid.NewGuid():n}@test.local";
        return await PostAsync<UserResponse>("/api/users", new CreateUserRequest
        {
            UserName = email,
            Email = email,
            Password = Password,
        });
    }

    private static async Task<HttpClient> CreateUserAndSignInAsync(string name)
    {
        var user = await CreateUserAsync(name);
        return await _factory.SignInAsync(user.UserName, Password);
    }

    private static Task GrantAsync(AccessSubject subjectType, string subjectId, AccessScope scope, string targetId, AccessLevel level)
        => PutAsync("/api/access/grants", new SetAccessGrantRequest
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            Scope = scope,
            TargetId = targetId,
            Level = level,
        });

    private static async Task<T> PostAsync<T>(string route, object body)
    {
        using var response = await _admin.PostAsJsonAsync(route, body);
        Assert.IsTrue(response.IsSuccessStatusCode,
            $"POST {route} → {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task PutAsync(string route, object body)
    {
        using var response = await _admin.PutAsJsonAsync(route, body);
        Assert.IsTrue(response.IsSuccessStatusCode,
            $"PUT {route} → {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
