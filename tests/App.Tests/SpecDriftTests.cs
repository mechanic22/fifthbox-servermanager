using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class SpecDriftTests
{
    private static WorkloadDeployment Expected(
        string image = "nginx:1.27",
        int replicas = 1,
        EnvVar[]? env = null,
        PortMapping[]? ports = null) => new()
        {
            Name = "fbsm--web",
            Image = image,
            Replicas = replicas,
            Env = env ?? [],
            Ports = ports ?? [],
        };

    private static DeployedSpec Live(
        string image = "nginx:1.27",
        int replicas = 1,
        string[]? env = null,
        string[]? ports = null,
        string[]? runningPorts = null) => new()
        {
            Image = image,
            Replicas = replicas,
            Env = env ?? [],
            Ports = ports ?? [],
            RunningPorts = runningPorts ?? [],
        };

    [TestMethod]
    public void A_matching_service_has_no_drift() =>
        Assert.IsEmpty(SpecDrift.Compare(Expected(), Live()));

    [TestMethod]
    public void The_resolved_digest_is_not_drift()
    {
        // swarm pins the resolved tag, so every service reads back with a digest
        Assert.IsEmpty(SpecDrift.Compare(
            Expected("nginx:1.27"),
            Live("nginx:1.27@sha256:0000000000000000000000000000000000000000000000000000000000000000")));
    }

    [TestMethod]
    public void An_implied_latest_tag_is_not_drift() =>
        Assert.IsEmpty(SpecDrift.Compare(Expected("nginx"), Live("nginx:latest")));

    [TestMethod]
    public void A_registry_port_is_not_mistaken_for_a_tag() =>
        Assert.IsEmpty(SpecDrift.Compare(
            Expected("registry.example.com:5000/app"),
            Live("registry.example.com:5000/app:latest")));

    [TestMethod]
    public void A_hand_changed_image_is_drift()
    {
        var drift = SpecDrift.Compare(Expected("nginx:1.27"), Live("nginx:1.29"));

        Assert.ContainsSingle(drift, "image");
    }

    [TestMethod]
    public void An_added_environment_variable_is_drift()
    {
        // docker service update --env-add, which we used to miss
        var drift = SpecDrift.Compare(
            Expected(env: [new EnvVar("A", "1")]),
            Live(env: ["A=1", "DEBUG=true"]));

        Assert.ContainsSingle(drift, "environment");
    }

    [TestMethod]
    public void Environment_order_is_not_drift() =>
        Assert.IsEmpty(SpecDrift.Compare(
            Expected(env: [new EnvVar("A", "1"), new EnvVar("B", "2")]),
            Live(env: ["B=2", "A=1"])));

    [TestMethod]
    public void A_hand_scaled_service_is_drift()
    {
        var drift = SpecDrift.Compare(Expected(replicas: 2), Live(replicas: 5));

        Assert.ContainsSingle(drift, "replicas");
    }

    [TestMethod]
    public void A_both_protocol_mapping_matches_its_two_published_ports() =>
        Assert.IsEmpty(SpecDrift.Compare(
            Expected(ports: [new PortMapping(53, 53, PortProtocol.Both)]),
            Live(ports: ["53:53/tcp", "53:53/udp"])));

    [TestMethod]
    public void A_container_still_bound_to_the_old_port_is_drift()
    {
        // host-mode ports are published by the container, so the service can hold the new mapping
        // while the running one never got it
        var drift = SpecDrift.Compare(
            Expected(ports: [new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host)]),
            Live(ports: ["27017:27017/tcp"], runningPorts: ["57017:27017/tcp"]));

        Assert.ContainsSingle(drift, "ports");
    }

    [TestMethod]
    public void Running_ports_that_match_are_not_drift() =>
        Assert.IsEmpty(SpecDrift.Compare(
            Expected(ports: [new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host)]),
            Live(ports: ["27017:27017/tcp"], runningPorts: ["27017:27017/tcp"])));

    [TestMethod]
    public void A_backend_that_reports_no_running_ports_is_judged_on_the_spec_alone() =>
        Assert.IsEmpty(SpecDrift.Compare(
            Expected(ports: [new PortMapping(8080, 80, PortProtocol.Tcp)]),
            Live(ports: ["8080:80/tcp"])));

    [TestMethod]
    public void Several_changes_are_all_reported()
    {
        var drift = SpecDrift.Compare(
            Expected("nginx:1.27", replicas: 1),
            Live("nginx:1.29", replicas: 3));

        CollectionAssert.AreEquivalent(new[] { "image", "replicas" }, drift.ToArray());
    }
}
