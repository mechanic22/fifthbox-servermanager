using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

[TestClass]
public class WorkloadHealthCheckTests
{
    private static WorkloadDeployment Deployment(Action<WorkloadDeploymentBuilder>? configure = null)
    {
        var builder = new WorkloadDeploymentBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private sealed class WorkloadDeploymentBuilder
    {
        public string? HealthCommand { get; set; }
        public int Interval { get; set; } = 10;
        public int Timeout { get; set; } = 3;
        public int Retries { get; set; } = 3;
        public int StartPeriod { get; set; } = 10;
        public bool PinToControlNode { get; set; }
        public List<PortMapping> Ports { get; set; } = [];
        public List<VolumeMount> Mounts { get; set; } = [];
        public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.OnFailure;

        public WorkloadDeployment Build() => new()
        {
            Name = "web",
            Image = "nginx:1.27",
            Replicas = 1,
            Ports = Ports,
            Mounts = Mounts,
            PinToControlNode = PinToControlNode,
            RestartPolicy = RestartPolicy,
            HealthCommand = HealthCommand,
            HealthIntervalSeconds = Interval,
            HealthTimeoutSeconds = Timeout,
            HealthRetries = Retries,
            HealthStartPeriodSeconds = StartPeriod,
        };
    }

    [TestMethod]
    public void No_health_command_means_no_healthcheck_on_the_spec()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(Deployment(), networkId: null);

        Assert.IsNull(spec.TaskTemplate.ContainerSpec.Healthcheck);
    }

    [TestMethod]
    public void A_health_command_runs_through_the_shell_with_its_timings()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(Deployment(b =>
        {
            b.HealthCommand = "curl -f http://localhost/health";
            b.Interval = 15;
            b.Timeout = 4;
            b.Retries = 2;
            b.StartPeriod = 30;
        }), networkId: null);

        var health = spec.TaskTemplate.ContainerSpec.Healthcheck!;
        CollectionAssert.AreEqual(new[] { "CMD-SHELL", "curl -f http://localhost/health" }, health.Test.ToList());
        Assert.AreEqual(TimeSpan.FromSeconds(15), health.Interval);
        Assert.AreEqual(TimeSpan.FromSeconds(4), health.Timeout);
        Assert.AreEqual(2, health.Retries);
        Assert.AreEqual((long)TimeSpan.FromSeconds(30).TotalNanoseconds, health.StartPeriod);
    }

    [TestMethod]
    public void Without_a_health_check_a_failed_rollout_pauses_rather_than_rolling_back()
    {
        // nothing tells swarm the task is bad so rollback never fires, pause at least stops it replacing every replica
        Assert.AreEqual("pause", WorkloadSpecMapper.ToUpdateConfig(Deployment()).FailureAction);
    }

    [TestMethod]
    public void With_a_health_check_a_failed_rollout_rolls_back()
    {
        var config = WorkloadSpecMapper.ToUpdateConfig(Deployment(b => b.HealthCommand = "true"));

        Assert.AreEqual("rollback", config.FailureAction);
        Assert.AreEqual(0, config.MaxFailureRatio);
    }

    [TestMethod]
    public void Monitor_covers_the_start_period_plus_every_retry()
    {
        var config = WorkloadSpecMapper.ToUpdateConfig(Deployment(b =>
        {
            b.HealthCommand = "true";
            b.StartPeriod = 20;
            b.Interval = 5;
            b.Retries = 4;
        }));

        Assert.AreEqual((long)TimeSpan.FromSeconds(40).TotalNanoseconds, config.Monitor);
    }

    [TestMethod]
    public void An_ordinary_workload_starts_the_new_task_before_stopping_the_old_one()
    {
        Assert.AreEqual("start-first", WorkloadSpecMapper.ToUpdateConfig(Deployment()).Order);
    }

    [DataRow(true, false, DisplayName = "platform service on the control node")]
    [DataRow(false, true, DisplayName = "host-mode published port")]
    [TestMethod]
    public void Anything_bound_to_a_host_port_must_stop_first(bool pinned, bool hostPort)
    {
        // two tasks can't share a host port, so start-first wouldn't schedule
        var config = WorkloadSpecMapper.ToUpdateConfig(Deployment(b =>
        {
            b.PinToControlNode = pinned;
            b.Ports = hostPort ? [new PortMapping(80, 80, PortProtocol.Tcp, PortPublishMode.Host)] : [];
        }));

        Assert.AreEqual("stop-first", config.Order);
    }

    [TestMethod]
    public void A_workload_with_a_mount_must_stop_first()
    {
        // regression: dropping the last port flipped a db to start-first and the second container died on the data dir lock
        var config = WorkloadSpecMapper.ToUpdateConfig(Deployment(b =>
        {
            b.Ports = [];
            b.Mounts = [new VolumeMount(VolumeMountType.Volume, "data", "/data/db", false)];
        }));

        Assert.AreEqual("stop-first", config.Order);
    }

    [DataRow(RestartPolicy.Never, "none")]
    [DataRow(RestartPolicy.OnFailure, "on-failure")]
    [DataRow(RestartPolicy.Always, "any")]
    [TestMethod]
    public void Restart_policy_reaches_the_task_spec(RestartPolicy policy, string expected)
    {
        // swarm defaults to always restarting, which contradicts Never
        var spec = WorkloadSpecMapper.ToServiceSpec(Deployment(b => b.RestartPolicy = policy), networkId: null);

        Assert.AreEqual(expected, spec.TaskTemplate.RestartPolicy.Condition);
    }
}
