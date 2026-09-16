using FifthBox.ServerManager.App.Cluster;
using FifthBox.ServerManager.Integrations.Swarm;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

/// shapes copied from a live docker 29.1.3 capture, services use "name", containers the swarm label
[TestClass]
public class SwarmEventInterpreterTests
{
    private static DockerModels.Message Event(string type, string action, params (string Key, string Value)[] attributes)
        => new()
        {
            Type = type,
            Action = action,
            Actor = new DockerModels.Actor
            {
                ID = "actor-id",
                Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
            },
        };

    [TestMethod]
    public void Node_events_map_to_a_node_change()
    {
        var change = SwarmEventInterpreter.Interpret(Event("node", "update", ("name", "worker-1")));

        Assert.IsNotNull(change);
        Assert.AreEqual(SwarmChangeKind.Node, change.Value.Kind);
        Assert.IsNull(change.Value.ServiceName);
    }

    [TestMethod]
    public void Service_events_map_to_a_workload_change_via_the_name_attribute()
    {
        var change = SwarmEventInterpreter.Interpret(Event("service", "create", ("name", "fbsm--web-01")));

        Assert.IsNotNull(change);
        Assert.AreEqual(SwarmChangeKind.Workload, change.Value.Kind);
        Assert.AreEqual("fbsm--web-01", change.Value.ServiceName);
    }

    [TestMethod]
    public void Container_events_map_via_the_swarm_service_label()
    {
        var change = SwarmEventInterpreter.Interpret(Event(
            "container",
            "health_status: unhealthy",
            ("com.docker.swarm.service.name", "fbsm--web-01"),
            ("com.docker.swarm.task.name", "fbsm--web-01.1.abc")));

        Assert.IsNotNull(change);
        Assert.AreEqual(SwarmChangeKind.Workload, change.Value.Kind);
        Assert.AreEqual("fbsm--web-01", change.Value.ServiceName);
    }

    [TestMethod]
    public void A_container_that_is_not_swarm_managed_is_ignored()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("container", "die", ("name", "some-local-container"))));
    }

    // fbsm-nginx/fbsm-host use a single dash, only fbsm-- is a workload
    [TestMethod]
    public void Platform_services_are_ignored()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("service", "update", ("name", "fbsm-nginx"))));
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("service", "update", ("name", "fbsm-host"))));
    }

    [TestMethod]
    public void Services_belonging_to_someone_else_on_the_daemon_are_ignored()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("service", "create", ("name", "web-01"))));
    }

    [TestMethod]
    public void The_prefix_alone_is_not_a_workload()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("service", "create", ("name", "fbsm--"))));
    }

    [TestMethod]
    public void Unrelated_object_types_are_ignored()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("network", "connect", ("name", "fbsm--web-01"))));
        Assert.IsNull(SwarmEventInterpreter.Interpret(Event("volume", "create", ("name", "fbsm--web-01"))));
    }

    [TestMethod]
    public void An_event_without_an_actor_is_ignored()
    {
        Assert.IsNull(SwarmEventInterpreter.Interpret(new DockerModels.Message { Type = "service", Action = "create" }));
    }
}
