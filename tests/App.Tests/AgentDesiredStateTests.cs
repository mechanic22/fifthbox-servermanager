using FifthBox.ServerManager.App.Cluster;
using Microsoft.Extensions.Options;
using FifthBox.ServerManager.App.Registries;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AgentDesiredStateTests
{
    private static AgentDesiredState Build(params Workload[] workloads)
    {
        var repo = new Mock<IWorkloadRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(workloads);
        var protector = new PassThroughProtector();
        var cluster = Options.Create(new ClusterOptions { OverlayNetwork = "fbsm-overlay" });
        return new AgentDesiredState(repo.Object, new WorkloadDeploymentFactory(protector, cluster));
    }

    private static Workload Native(string name, string agentId, params WorkloadRevision[] revisions) => new()
    {
        Id = name, Name = name, Kind = WorkloadKind.Native, AgentId = agentId,
        Command = "/desired", Revisions = [.. revisions],
    };

    [TestMethod]
    public async Task Secret_env_reaches_the_agent_decrypted()
    {
        // regression: reconcile sent the stored ciphertext as the secret, only showed up after an agent reconnect
        var state = Build(Native("srcds", "a1", new WorkloadRevision
        {
            Number = 1, Command = "/srv/srcds",
            Env = [new EnvVar("RCON_PASSWORD", "enc:hunter2", Secret: true), new EnvVar("PORT", "27015")],
        }));

        var spec = (await state.ForAgentAsync("a1")).Single();

        Assert.AreEqual("hunter2", spec.Env.Single(e => e.Key == "RCON_PASSWORD").Value);
        Assert.AreEqual("27015", spec.Env.Single(e => e.Key == "PORT").Value, "a plain value is passed through untouched");
    }

    [TestMethod]
    public async Task A_steam_password_reaches_the_agent_decrypted()
    {
        var state = Build(Native("srcds", "a1", new WorkloadRevision
        {
            Number = 1, Command = "/srv/srcds",
            Source = new WorkloadSource
            {
                Kind = SourceKind.SteamCmd, SteamAppId = 740, SteamUsername = "me", SteamPasswordEnc = "enc:hunter2",
            },
        }));

        var spec = (await state.ForAgentAsync("a1")).Single();

        Assert.AreEqual("hunter2", spec.Source!.SteamPassword);
        Assert.AreEqual(740, spec.Source.SteamAppId);
    }

    [TestMethod]
    public async Task Returns_only_workloads_for_the_asking_agent()
    {
        var state = Build(
            Native("mine", "a1", new WorkloadRevision { Number = 1, Command = "/mine" }),
            Native("theirs", "a2", new WorkloadRevision { Number = 1, Command = "/theirs" }));

        var desired = await state.ForAgentAsync("a1");

        Assert.AreEqual("mine", desired.Single().Name);
    }

    [TestMethod]
    public async Task Skips_workloads_that_were_never_deployed()
    {
        var state = Build(Native("saved-only", "a1"));

        Assert.IsEmpty(await state.ForAgentAsync("a1"), "a saved definition is not desired state");
    }

    [TestMethod]
    public async Task Skips_stopped_workloads()
    {
        var workload = Native("srcds", "a1", new WorkloadRevision { Number = 1, Command = "/srcds" });
        workload.DesiredState = WorkloadDesiredState.Stopped;

        Assert.IsEmpty(await Build(workload).ForAgentAsync("a1"), "a reconnect must not undo a stop");
    }

    [TestMethod]
    public async Task Skips_container_workloads()
    {
        var container = new Workload
        {
            Id = "web", Name = "web", Kind = WorkloadKind.Container, AgentId = "a1", Image = "nginx",
            Revisions = [new WorkloadRevision { Number = 1, Image = "nginx" }],
        };

        Assert.IsEmpty(await Build(container).ForAgentAsync("a1"));
    }

    [TestMethod]
    public async Task Uses_the_newest_revision_not_the_saved_config()
    {
        var workload = Native("srcds", "a1",
            new WorkloadRevision { Number = 1, Command = "/v1" },
            new WorkloadRevision { Number = 2, Command = "/v2" });
        workload.Command = "/unsaved-edit";

        var spec = (await Build(workload).ForAgentAsync("a1")).Single();

        Assert.AreEqual("/v2", spec.Command, "reconcile must not apply edits the operator hasn't deployed");
    }

    [TestMethod]
    public async Task Carries_restart_policy_and_stop_grace_from_the_revision()
    {
        var workload = Native("srcds", "a1", new WorkloadRevision
        {
            Number = 1, Command = "/srv/srcds", Args = ["-game", "csgo"], WorkingDirectory = "/srv",
            RestartPolicy = RestartPolicy.Always, StopGraceSeconds = 45, StopCommand = "quit", ManagedDirectory = true,
            Env = [new EnvVar("PORT", "27015")],
        });

        var spec = (await Build(workload).ForAgentAsync("a1")).Single();

        Assert.AreEqual(RestartPolicy.Always, spec.RestartPolicy);
        Assert.AreEqual(45, spec.StopGraceSeconds);
        Assert.AreEqual("quit", spec.StopCommand);
        Assert.IsTrue(spec.ManagedDirectory);
        Assert.AreEqual("/srv", spec.WorkingDirectory);
        CollectionAssert.AreEqual(new[] { "-game", "csgo" }, spec.Args.ToArray());
        Assert.AreEqual("PORT", spec.Env.Single().Key);
    }

    /// makes a missed decrypt on the reconcile path visible
    private sealed class PassThroughProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"enc:{plaintext}";

        public string Unprotect(string ciphertext) => ciphertext.StartsWith("enc:", StringComparison.Ordinal)
            ? ciphertext["enc:".Length..]
            : ciphertext;
    }
}
