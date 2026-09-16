using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Workloads;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

[TestClass]
public class WorkloadSpecMapperTests
{
    [TestMethod]
    public void ToServiceSpec_maps_name_image_env_ports_replicas_and_network()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web",
            Image = "nginx:1.27",
            Replicas = 3,
            Env = [new EnvVar("KEY", "VAL")],
            Ports = [new PortMapping(8080, 80, PortProtocol.Tcp), new PortMapping(53, 53, PortProtocol.Udp)],
        }, networkId: "netid");

        Assert.AreEqual("web", spec.Name);
        Assert.AreEqual("nginx:1.27", spec.TaskTemplate.ContainerSpec.Image);
        CollectionAssert.Contains(spec.TaskTemplate.ContainerSpec.Env.ToList(), "KEY=VAL");
        Assert.AreEqual(3ul, spec.Mode.Replicated.Replicas!.Value);

        Assert.HasCount(1, spec.TaskTemplate.Networks);
        Assert.AreEqual("netid", spec.TaskTemplate.Networks[0].Target);

        Assert.HasCount(2, spec.EndpointSpec.Ports);
        var tcp = spec.EndpointSpec.Ports.First(p => p.PublishedPort == 8080);
        Assert.AreEqual("tcp", tcp.Protocol);
        Assert.AreEqual(80u, tcp.TargetPort);
        var udp = spec.EndpointSpec.Ports.First(p => p.Protocol == "udp");
        Assert.AreEqual(53u, udp.PublishedPort);
    }

    [TestMethod]
    public void ForceUpdate_is_bumped_when_the_published_ports_change()
    {
        // ports live outside the task template, without the bump the container keeps its old port
        var live = Spec([new PortMapping(57017, 27017, PortProtocol.Tcp, PortPublishMode.Host)], forceUpdate: 4);
        var desired = Spec([new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host)]);

        Assert.AreEqual(5ul, WorkloadSpecMapper.ForceUpdateFor(live, desired));
    }

    [TestMethod]
    public void ForceUpdate_is_bumped_when_a_port_is_removed()
    {
        var live = Spec([new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host)], forceUpdate: 1);

        Assert.AreEqual(2ul, WorkloadSpecMapper.ForceUpdateFor(live, Spec([])));
    }

    [TestMethod]
    public void ForceUpdate_is_bumped_when_a_running_task_holds_a_port_the_spec_already_dropped()
    {
        // how it gets stuck: spec changed before the bump existed, so it agrees with us but the container's on the old port
        var ports = new[] { new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host) };
        var stale = RunningTask(published: 57017, target: 27017);

        Assert.AreEqual(1ul, WorkloadSpecMapper.ForceUpdateFor(Spec(ports), Spec(ports), [stale]));
    }

    [TestMethod]
    public void ForceUpdate_ignores_tasks_that_are_not_running()
    {
        var ports = new[] { new PortMapping(27017, 27017, PortProtocol.Tcp, PortPublishMode.Host) };
        var shutdown = RunningTask(published: 57017, target: 27017);
        shutdown.Status.State = DockerModels.TaskState.Shutdown;

        Assert.AreEqual(0ul, WorkloadSpecMapper.ForceUpdateFor(Spec(ports), Spec(ports), [shutdown]));
    }

    private static DockerModels.TaskResponse RunningTask(int published, int target) => new()
    {
        Status = new DockerModels.TaskStatus
        {
            State = DockerModels.TaskState.Running,
            PortStatus = new DockerModels.PortStatus
            {
                Ports =
                [
                    new DockerModels.PortConfig
                    {
                        PublishedPort = (uint)published,
                        TargetPort = (uint)target,
                        Protocol = "tcp",
                        PublishMode = "host",
                    },
                ],
            },
        },
    };

    [TestMethod]
    public void ForceUpdate_carries_forward_when_the_ports_are_unchanged()
    {
        // carried, not reset, resetting is a template change so every deploy after a Restart would recreate tasks
        var ports = new[] { new PortMapping(8080, 80, PortProtocol.Tcp) };
        var live = Spec(ports, forceUpdate: 7);

        Assert.AreEqual(7ul, WorkloadSpecMapper.ForceUpdateFor(live, Spec(ports)));
    }

    [TestMethod]
    public void ForceUpdate_starts_at_zero_for_a_service_that_is_not_there()
        => Assert.AreEqual(0ul, WorkloadSpecMapper.ForceUpdateFor(null, Spec([])));

    private static DockerModels.ServiceSpec Spec(IReadOnlyList<PortMapping> ports, ulong forceUpdate = 0)
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "db",
            Image = "mongo:4.4",
            Replicas = 1,
            Ports = [.. ports],
        }, networkId: null);

        spec.TaskTemplate.ForceUpdate = forceUpdate;
        return spec;
    }

    [TestMethod]
    public void ToServiceSpec_sets_resource_limits_only_when_present()
    {
        var withLimits = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1, MemoryLimitMb = 512, CpuLimit = 0.5,
        }, networkId: null);

        Assert.IsNotNull(withLimits.TaskTemplate.Resources?.Limits);
        Assert.AreEqual(512L * 1024 * 1024, withLimits.TaskTemplate.Resources!.Limits!.MemoryBytes);
        Assert.AreEqual(500_000_000L, withLimits.TaskTemplate.Resources.Limits.NanoCPUs);

        var noLimits = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1,
        }, networkId: null);

        Assert.IsNull(noLimits.TaskTemplate.Resources);
    }

    [TestMethod]
    public void ToServiceSpec_sets_reservations_independently_of_limits()
    {
        // a reservation without a limit is normal, set space aside without capping
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1, MemoryReserveMb = 256, CpuReserve = 0.25,
        }, networkId: null);

        Assert.IsNull(spec.TaskTemplate.Resources!.Limits);
        Assert.AreEqual(256L * 1024 * 1024, spec.TaskTemplate.Resources.Reservations!.MemoryBytes);
        Assert.AreEqual(250_000_000L, spec.TaskTemplate.Resources.Reservations.NanoCPUs);
    }

    [TestMethod]
    public void ToServiceSpec_carries_limits_and_reservations_together()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1,
            MemoryLimitMb = 512, CpuLimit = 1, MemoryReserveMb = 256, CpuReserve = 0.5,
        }, networkId: null);

        Assert.AreEqual(512L * 1024 * 1024, spec.TaskTemplate.Resources!.Limits!.MemoryBytes);
        Assert.AreEqual(256L * 1024 * 1024, spec.TaskTemplate.Resources.Reservations!.MemoryBytes);
        Assert.AreEqual(1_000_000_000L, spec.TaskTemplate.Resources.Limits.NanoCPUs);
        Assert.AreEqual(500_000_000L, spec.TaskTemplate.Resources.Reservations.NanoCPUs);
    }

    [TestMethod]
    public void ToServiceSpec_asks_for_a_global_service_when_told_to()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "agent", Image = "exporter", Mode = WorkloadMode.Global, Replicas = 4,
        }, networkId: null);

        Assert.IsNotNull(spec.Mode.Global);
        Assert.IsNull(spec.Mode.Replicated, "a replica count on a global service is meaningless");
    }

    [TestMethod]
    public void A_global_services_desired_count_is_the_tasks_swarm_still_wants()
    {
        // swarm has no replica count for global services, reading Mode.Replicated shows Stopped 0/0
        var spec = new DockerModels.ServiceSpec { Mode = new DockerModels.ServiceMode { Global = new DockerModels.GlobalService() } };
        DockerModels.TaskResponse Task(DockerModels.TaskState desired) => new() { DesiredState = desired };

        var desired = WorkloadSpecMapper.DesiredCount(spec,
        [
            Task(DockerModels.TaskState.Running),
            Task(DockerModels.TaskState.Running),
            Task(DockerModels.TaskState.Shutdown),
        ]);

        Assert.AreEqual(2, desired);
    }

    [TestMethod]
    public void Every_workload_is_constrained_to_its_images_os()
    {
        // without this swarm puts a linux image on a windows node and lets it fail
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1,
        }, networkId: null);

        CollectionAssert.Contains(spec.TaskTemplate.Placement!.Constraints.ToArray(), "node.platform.os == linux");
    }

    [TestMethod]
    public void ToServiceSpec_expands_both_protocol_into_tcp_and_udp()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "game", Image = "srv", Replicas = 1,
            Ports = [new PortMapping(7777, 7777, PortProtocol.Both)],
        }, networkId: null);

        var ports = spec.EndpointSpec.Ports;
        Assert.HasCount(2, ports);
        Assert.IsTrue(ports.Any(p => p.Protocol == "tcp" && p.PublishedPort == 7777));
        Assert.IsTrue(ports.Any(p => p.Protocol == "udp" && p.PublishedPort == 7777));
    }

    [TestMethod]
    public void ToServiceSpec_applies_service_labels_when_present()
    {
        var labelled = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "fbsm-nginx", Image = "nginx", Replicas = 1,
            Labels = new Dictionary<string, string> { ["fbsm.role"] = "platform" },
        }, networkId: null);

        Assert.IsNotNull(labelled.Labels);
        Assert.AreEqual("platform", labelled.Labels["fbsm.role"]);

        var plain = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment { Name = "web", Image = "nginx", Replicas = 1 }, networkId: null);
        Assert.IsNull(plain.Labels);
    }

    [TestMethod]
    public void ToServiceSpec_maps_volume_and_bind_mounts()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web", Image = "nginx", Replicas = 1,
            Mounts =
            [
                new VolumeMount(VolumeMountType.Volume, "data", "/var/lib/data", ReadOnly: false),
                new VolumeMount(VolumeMountType.Bind, "/host/site", "/usr/share/nginx/html", ReadOnly: true),
            ],
        }, networkId: null);

        var mounts = spec.TaskTemplate.ContainerSpec.Mounts;
        Assert.HasCount(2, mounts);

        var vol = mounts.First(m => m.Target == "/var/lib/data");
        Assert.AreEqual("volume", vol.Type);
        Assert.AreEqual("web--data", vol.Source, "named volumes are scoped to the service");
        Assert.IsFalse(vol.ReadOnly);

        var bind = mounts.First(m => m.Target == "/usr/share/nginx/html");
        Assert.AreEqual("bind", bind.Type);
        Assert.AreEqual("/host/site", bind.Source);
        Assert.IsTrue(bind.ReadOnly);
    }

    [TestMethod]
    public void ToServiceSpec_uses_publish_mode_per_port()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "web",
            Image = "nginx",
            Ports =
            [
                new PortMapping(8080, 80, PortProtocol.Tcp, PortPublishMode.Host),
                new PortMapping(9090, 90, PortProtocol.Tcp, PortPublishMode.Ingress),
            ],
        }, networkId: null);

        Assert.AreEqual("host", spec.EndpointSpec.Ports.First(p => p.PublishedPort == 8080).PublishMode);
        Assert.AreEqual("ingress", spec.EndpointSpec.Ports.First(p => p.PublishedPort == 9090).PublishMode);
    }

    [TestMethod]
    public void ToServiceSpec_pins_to_node_and_mounts_configs()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(
            new WorkloadDeployment { Name = "fbsm-nginx", Image = "nginx" },
            networkId: "net",
            pinnedNodeId: "node123",
            configs: [new ResolvedConfig("cid", "fbsm-nginx-nginx-abc123", "/etc/nginx/conf.d/default.conf")]);

        Assert.IsNotNull(spec.TaskTemplate.Placement);
        CollectionAssert.Contains(spec.TaskTemplate.Placement.Constraints.ToList(), "node.id == node123");

        Assert.HasCount(1, spec.TaskTemplate.ContainerSpec.Configs);
        var config = spec.TaskTemplate.ContainerSpec.Configs[0];
        Assert.AreEqual("cid", config.ConfigID);
        Assert.AreEqual("fbsm-nginx-nginx-abc123", config.ConfigName);
        Assert.AreEqual("/etc/nginx/conf.d/default.conf", config.File.Name);
    }

    [TestMethod]
    public void ConfigObjectName_is_deterministic_by_content()
    {
        var a = WorkloadSpecMapper.ConfigObjectName("svc", "nginx", "content-1");
        var b = WorkloadSpecMapper.ConfigObjectName("svc", "nginx", "content-1");
        var c = WorkloadSpecMapper.ConfigObjectName("svc", "nginx", "content-2");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
        StringAssert.StartsWith(a, "svc-nginx-");
    }

    [TestMethod]
    public void ToServiceSpec_without_a_network_has_no_attachments()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment { Name = "x", Image = "img" }, networkId: null);
        Assert.IsEmpty(spec.TaskTemplate.Networks);
    }

    [TestMethod]
    public void ToServiceSpec_scopes_named_volumes_but_not_bind_mounts()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(new WorkloadDeployment
        {
            Name = "fbsm--grafana",
            Image = "grafana/grafana",
            Replicas = 1,
            Mounts =
            [
                new VolumeMount(VolumeMountType.Volume, "data", "/var/lib/grafana", false),
                new VolumeMount(VolumeMountType.Bind, "/srv/media", "/media", true),
            ],
        }, networkId: null);

        var volume = spec.TaskTemplate.ContainerSpec.Mounts.Single(m => m.Type == "volume");
        var bind = spec.TaskTemplate.ContainerSpec.Mounts.Single(m => m.Type == "bind");

        Assert.AreEqual("fbsm--grafana--data", volume.Source, "two workloads asking for 'data' get their own");
        Assert.AreEqual("/srv/media", bind.Source, "a host path is the operator's choice — never rewritten");
    }

    [TestMethod]
    public void ToServiceSpec_mounts_secrets_read_only_by_root()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(
            new WorkloadDeployment { Name = "fbsm-nginx", Image = "nginx", Replicas = 1 },
            networkId: null,
            pinnedNodeId: null,
            configs: null,
            secrets: [new ResolvedSecret("sec1", "fbsm-nginx-key-app.example.com-abc123", "key-app.example.com.pem")]);

        var secret = spec.TaskTemplate.ContainerSpec.Secrets.Single();
        Assert.AreEqual("sec1", secret.SecretID);
        Assert.AreEqual("fbsm-nginx-key-app.example.com-abc123", secret.SecretName);
        Assert.AreEqual("key-app.example.com.pem", secret.File.Name);
        // 0400, anything that can read the key has the key
        Assert.AreEqual(0b100_000_000u, secret.File.Mode);
    }

    [TestMethod]
    public void ToServiceSpec_leaves_secrets_unset_when_there_are_none()
    {
        var spec = WorkloadSpecMapper.ToServiceSpec(
            new WorkloadDeployment { Name = "web", Image = "nginx", Replicas = 1 }, networkId: null);

        Assert.IsNull(spec.TaskTemplate.ContainerSpec.Secrets);
    }

    [TestMethod]
    public void SecretObjectName_changes_with_content_so_a_renewal_rolls_the_service()
    {
        var before = WorkloadSpecMapper.SecretObjectName("fbsm-nginx", "key-app.example.com", "old-key");
        var after = WorkloadSpecMapper.SecretObjectName("fbsm-nginx", "key-app.example.com", "new-key");

        // swarm secrets are immutable so a renewed key needs a new object, same content keeps the name so redeploys are no-ops
        Assert.AreNotEqual(before, after);
        Assert.AreEqual(before, WorkloadSpecMapper.SecretObjectName("fbsm-nginx", "key-app.example.com", "old-key"));
        StringAssert.StartsWith(before, "fbsm-nginx-key-app.example.com-");
    }

    [TestMethod]
    public void ToRuntimeStatus_not_deployed()
    {
        var status = WorkloadSpecMapper.ToRuntimeStatus("x", deployed: false, desired: 0, running: 0);
        Assert.AreEqual(WorkloadState.NotDeployed, status.State);
        Assert.IsFalse(status.Deployed);
    }

    [TestMethod]
    public void ToRuntimeStatus_running_when_all_replicas_up()
        => Assert.AreEqual(WorkloadState.Running, WorkloadSpecMapper.ToRuntimeStatus("x", true, 3, 3).State);

    [TestMethod]
    public void ToRuntimeStatus_partial_when_some_replicas_down()
        => Assert.AreEqual(WorkloadState.Partial, WorkloadSpecMapper.ToRuntimeStatus("x", true, 3, 1).State);

    [TestMethod]
    public void ToRuntimeStatus_stopped_when_desired_zero()
        => Assert.AreEqual(WorkloadState.Stopped, WorkloadSpecMapper.ToRuntimeStatus("x", true, 0, 0).State);
}
