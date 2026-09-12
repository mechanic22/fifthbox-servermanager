using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class ProcessAdoptionTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Same_start_time_is_the_same_process()
    {
        Assert.IsTrue(ProcessAdoption.IsSameProcess(Start, Start));
    }

    [TestMethod]
    public void Sub_second_drift_still_matches()
    {
        Assert.IsTrue(ProcessAdoption.IsSameProcess(Start, Start.AddMilliseconds(400)));
    }

    [TestMethod]
    public void A_recycled_pid_started_later_is_rejected()
    {
        Assert.IsFalse(ProcessAdoption.IsSameProcess(Start, Start.AddMinutes(5)),
            "adopting a reused PID would kill an unrelated process");
    }

    [TestMethod]
    public void A_pid_started_earlier_than_recorded_is_rejected()
    {
        Assert.IsFalse(ProcessAdoption.IsSameProcess(Start, Start.AddHours(-2)));
    }

    private static AgentWorkloadSpec Spec(Action<SpecBuilder>? tweak = null)
    {
        var b = new SpecBuilder();
        tweak?.Invoke(b);
        return new AgentWorkloadSpec
        {
            Name = "srcds",
            Command = b.Command,
            Args = b.Args,
            WorkingDirectory = b.WorkingDirectory,
            Env = b.Env,
            RestartPolicy = b.RestartPolicy,
            StopGraceSeconds = b.StopGraceSeconds,
            StopCommand = b.StopCommand,
            ManagedDirectory = b.ManagedDirectory,
        };
    }

    private sealed class SpecBuilder
    {
        public string Command { get; set; } = "/srv/srcds";
        public IReadOnlyList<string> Args { get; set; } = ["-game", "csgo"];
        public string? WorkingDirectory { get; set; } = "/srv";
        public IReadOnlyList<EnvVar> Env { get; set; } = [new EnvVar("PORT", "27015")];
        public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;
        public int StopGraceSeconds { get; set; } = 10;
        public string? StopCommand { get; set; } = "quit";
        public bool ManagedDirectory { get; set; }
    }

    [TestMethod]
    public void The_hash_is_stable_across_equal_specs()
    {
        Assert.AreEqual(ProcessAdoption.HashOf(Spec()), ProcessAdoption.HashOf(Spec()));
    }

    [TestMethod]
    public void The_hash_moves_with_any_config_change()
    {
        var baseline = ProcessAdoption.HashOf(Spec());

        Assert.AreNotEqual(baseline, ProcessAdoption.HashOf(Spec(b => b.Command = "/srv/other")));
        Assert.AreNotEqual(baseline, ProcessAdoption.HashOf(Spec(b => b.StopCommand = "stop")));
        Assert.AreNotEqual(baseline, ProcessAdoption.HashOf(Spec(b => b.Env = [new EnvVar("PORT", "27016")])));
    }

    [TestMethod]
    public void The_hash_does_not_leak_the_environment_it_covers()
    {
        // The state file records this hash precisely so it need not record env values, so the hash must
        // not carry them either.
        var hash = ProcessAdoption.HashOf(Spec(b => b.Env = [new EnvVar("RCON_PASSWORD", "hunter2")]));

        StringAssert.DoesNotMatch(hash, new System.Text.RegularExpressions.Regex("hunter2"));
        StringAssert.DoesNotMatch(hash, new System.Text.RegularExpressions.Regex("RCON_PASSWORD"));
    }

    [TestMethod]
    public void Stripping_the_env_changes_the_hash_which_is_why_it_is_recorded_separately()
    {
        // The state file stores the spec without its env and the hash of the spec *with* it. Recomputing
        // from what was stored would not match, so adoption after an agent restart would stop and
        // restart a healthy game server instead of attaching to it.
        var full = Spec(b => b.Env = [new EnvVar("RCON_PASSWORD", "hunter2")]);
        var stripped = full with { Env = [] };

        Assert.AreNotEqual(ProcessAdoption.HashOf(full), ProcessAdoption.HashOf(stripped));
    }

    [TestMethod]
    public void Taking_over_the_directory_is_a_config_change()
    {
        // It changes where the process runs from and how its command resolves, so an adopted process on
        // the old layout must not be treated as already correct.
        Assert.IsFalse(ProcessAdoption.SameConfig(Spec(), Spec(b => b.ManagedDirectory = true)));
    }

    [TestMethod]
    public void A_changed_stop_command_is_a_config_change()
    {
        Assert.IsFalse(ProcessAdoption.SameConfig(Spec(), Spec(b => b.StopCommand = "stop")));
    }

    [TestMethod]
    public void Identical_specs_match_despite_being_different_list_instances()
    {
        Assert.IsTrue(ProcessAdoption.SameConfig(Spec(), Spec()));
    }

    [TestMethod]
    public void Env_order_does_not_count_as_a_config_change()
    {
        var a = Spec(b => b.Env = [new EnvVar("A", "1"), new EnvVar("B", "2")]);
        var b2 = Spec(b => b.Env = [new EnvVar("B", "2"), new EnvVar("A", "1")]);

        Assert.IsTrue(ProcessAdoption.SameConfig(a, b2));
    }

    [TestMethod]
    public void Arg_order_does_count_as_a_config_change()
    {
        var a = Spec(b => b.Args = ["-game", "csgo"]);
        var b2 = Spec(b => b.Args = ["csgo", "-game"]);

        Assert.IsFalse(ProcessAdoption.SameConfig(a, b2), "args are positional");
    }

    [TestMethod]
    public void A_changed_command_is_a_config_change()
    {
        Assert.IsFalse(ProcessAdoption.SameConfig(Spec(), Spec(b => b.Command = "/srv/other")));
    }

    [TestMethod]
    public void A_changed_restart_policy_is_a_config_change()
    {
        Assert.IsFalse(ProcessAdoption.SameConfig(Spec(), Spec(b => b.RestartPolicy = RestartPolicy.Always)));
    }

    [TestMethod]
    public void A_changed_env_value_is_a_config_change()
    {
        Assert.IsFalse(ProcessAdoption.SameConfig(Spec(), Spec(b => b.Env = [new EnvVar("PORT", "27016")])));
    }
}
