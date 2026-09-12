using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Workloads;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

/// The bulk sweep folds one service list and one task list into a status per service — the pass that
/// catches replicas dying on nodes whose container events the Host never sees.
[TestClass]
public class SwarmStatusMapperTests
{
    private static DockerModels.SwarmService Service(string id, string name, int replicas) => new()
    {
        ID = id,
        Spec = new DockerModels.ServiceSpec
        {
            Name = name,
            Mode = new DockerModels.ServiceMode
            {
                Replicated = new DockerModels.ReplicatedService { Replicas = (ulong)replicas },
            },
        },
    };

    private static DockerModels.TaskResponse Task(string serviceId, DockerModels.TaskState state) => new()
    {
        ServiceID = serviceId,
        Status = new DockerModels.TaskStatus { State = state },
    };

    [TestMethod]
    public void All_replicas_up_reads_as_running()
    {
        var statuses = SwarmStatusMapper.Derive(
            [Service("s1", "fbsm--web", 2)],
            [Task("s1", DockerModels.TaskState.Running), Task("s1", DockerModels.TaskState.Running)]);

        Assert.AreEqual(WorkloadState.Running, statuses["fbsm--web"].State);
        Assert.AreEqual(2, statuses["fbsm--web"].DesiredReplicas);
        Assert.AreEqual(2, statuses["fbsm--web"].RunningReplicas);
    }

    [TestMethod]
    public void A_replica_down_reads_as_partial()
    {
        var statuses = SwarmStatusMapper.Derive(
            [Service("s1", "fbsm--web", 3)],
            [Task("s1", DockerModels.TaskState.Running), Task("s1", DockerModels.TaskState.Running)]);

        Assert.AreEqual(WorkloadState.Partial, statuses["fbsm--web"].State);
        Assert.AreEqual(2, statuses["fbsm--web"].RunningReplicas);
    }

    [TestMethod]
    public void Shut_down_task_history_is_not_counted()
    {
        // A service restarted twice keeps its dead tasks. Counting them would report 3/1 running.
        var statuses = SwarmStatusMapper.Derive(
            [Service("s1", "fbsm--web", 1)],
            [
                Task("s1", DockerModels.TaskState.Shutdown),
                Task("s1", DockerModels.TaskState.Failed),
                Task("s1", DockerModels.TaskState.Running),
            ]);

        Assert.AreEqual(1, statuses["fbsm--web"].RunningReplicas);
        Assert.AreEqual(WorkloadState.Running, statuses["fbsm--web"].State);
    }

    [TestMethod]
    public void Tasks_are_attributed_to_their_own_service()
    {
        var statuses = SwarmStatusMapper.Derive(
            [Service("s1", "fbsm--a", 1), Service("s2", "fbsm--b", 1)],
            [Task("s1", DockerModels.TaskState.Running), Task("s2", DockerModels.TaskState.Shutdown)]);

        Assert.AreEqual(WorkloadState.Running, statuses["fbsm--a"].State);
        Assert.AreEqual(WorkloadState.Partial, statuses["fbsm--b"].State);
        Assert.AreEqual(0, statuses["fbsm--b"].RunningReplicas);
    }

    [TestMethod]
    public void A_service_scaled_to_zero_reads_as_stopped()
    {
        var statuses = SwarmStatusMapper.Derive([Service("s1", "fbsm--web", 0)], []);

        Assert.AreEqual(WorkloadState.Stopped, statuses["fbsm--web"].State);
    }

    [TestMethod]
    public void A_service_with_no_tasks_at_all_is_partial_not_missing()
    {
        // Deployed but nothing scheduled yet — it exists, so it must not read as NotDeployed.
        var statuses = SwarmStatusMapper.Derive([Service("s1", "fbsm--web", 1)], []);

        Assert.IsTrue(statuses["fbsm--web"].Deployed);
        Assert.AreEqual(WorkloadState.Partial, statuses["fbsm--web"].State);
    }

    [TestMethod]
    public void Update_status_rides_along_from_the_list()
    {
        var service = Service("s1", "fbsm--web", 1);
        service.UpdateStatus = new DockerModels.UpdateStatus { State = "rollback_completed", Message = "rolled back" };

        var statuses = SwarmStatusMapper.Derive([service], [Task("s1", DockerModels.TaskState.Running)]);

        Assert.AreEqual("rollback_completed", statuses["fbsm--web"].UpdateState);
        Assert.AreEqual("rolled back", statuses["fbsm--web"].UpdateMessage);
    }

    [TestMethod]
    public void Platform_and_foreign_services_are_still_returned()
    {
        // Filtering by prefix is the caller's job — this maps whatever the daemon reports.
        var statuses = SwarmStatusMapper.Derive(
            [Service("s1", "fbsm-nginx", 1), Service("s2", "someone-elses", 1)],
            []);

        Assert.HasCount(2, statuses);
    }

    [TestMethod]
    public void An_unnamed_service_is_skipped_rather_than_throwing()
        => Assert.IsEmpty(SwarmStatusMapper.Derive([new DockerModels.SwarmService { ID = "s1" }], []));

    [TestMethod]
    public void Nothing_deployed_is_an_empty_map()
        => Assert.IsEmpty(SwarmStatusMapper.Derive([], []));
}
