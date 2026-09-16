using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Workloads;
using DockerModels = Docker.DotNet.Models;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

/// the task list is the only thing that answers "why is it 0/1"
[TestClass]
public class SwarmTaskMapperTests
{
    private static DockerModels.TaskResponse Task(
        DockerModels.TaskState state,
        DateTime updatedAt,
        string? error = null,
        string? nodeId = null,
        long slot = 1,
        DockerModels.TaskState desired = DockerModels.TaskState.Running) => new()
        {
            ID = $"t{updatedAt.Ticks}",
            Slot = slot,
            NodeID = nodeId,
            UpdatedAt = updatedAt,
            DesiredState = desired,
            Status = new DockerModels.TaskStatus { State = state, Err = error, Timestamp = updatedAt },
        };

    private static DateTime At(int minute) => new(2026, 8, 24, 12, minute, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Newest_attempt_comes_first()
    {
        var tasks = SwarmTaskMapper.ToTasks(
        [
            Task(DockerModels.TaskState.Failed, At(1)),
            Task(DockerModels.TaskState.Running, At(3)),
            Task(DockerModels.TaskState.Failed, At(2)),
        ]);

        CollectionAssert.AreEqual(
            new[] { WorkloadTaskState.Running, WorkloadTaskState.Failed, WorkloadTaskState.Failed },
            tasks.Select(t => t.State).ToArray());
    }

    [TestMethod]
    public void Order_follows_the_timestamp_the_table_shows()
    {
        // UpdatedAt and Status.Timestamp are different clocks, the table shows the latter
        var older = Task(DockerModels.TaskState.Failed, At(1));
        older.UpdatedAt = At(9);
        var newer = Task(DockerModels.TaskState.Failed, At(5));
        newer.UpdatedAt = At(2);

        var tasks = SwarmTaskMapper.ToTasks([older, newer]);

        CollectionAssert.AreEqual(
            new[] { At(5), At(1) },
            tasks.Select(t => t.Since!.Value.UtcDateTime).ToArray());
    }

    [TestMethod]
    public void History_is_capped()
    {
        var tasks = SwarmTaskMapper.ToTasks(
            Enumerable.Range(1, 40).Select(i => Task(DockerModels.TaskState.Failed, At(i))));

        Assert.HasCount(SwarmTaskMapper.MaxTasks, tasks);
        // capped from the newest end
        Assert.AreEqual(At(40), tasks[0].Since!.Value.UtcDateTime);
    }

    [TestMethod]
    public void Placement_and_error_survive_the_projection()
    {
        var task = SwarmTaskMapper.ToTasks(
        [
            Task(DockerModels.TaskState.Rejected, At(1), error: "no suitable node (insufficient resources)", nodeId: "n1", slot: 3),
        ]).Single();

        Assert.AreEqual("n1", task.NodeId);
        Assert.AreEqual(3, task.Slot);
        Assert.AreEqual("no suitable node (insufficient resources)", task.Error);
        Assert.AreEqual(WorkloadTaskState.Rejected, task.State);
        Assert.AreEqual(WorkloadTaskState.Running, task.DesiredState);
    }

    [TestMethod]
    public void Last_error_reports_the_newest_failure()
    {
        var tasks = SwarmTaskMapper.ToTasks(
        [
            Task(DockerModels.TaskState.Failed, At(1), error: "older"),
            Task(DockerModels.TaskState.Failed, At(2), error: "newer"),
        ]);

        Assert.AreEqual("newer", SwarmTaskMapper.LastError(tasks));
    }

    [TestMethod]
    public void A_healthy_service_does_not_report_its_old_failures()
    {
        // swarm marks the superseded attempt desired-Shutdown, reporting it leaves a permanent error on a recovered service
        var tasks = SwarmTaskMapper.ToTasks(
        [
            Task(DockerModels.TaskState.Failed, At(1), error: "crashed once", desired: DockerModels.TaskState.Shutdown),
            Task(DockerModels.TaskState.Running, At(2)),
        ]);

        Assert.IsNull(SwarmTaskMapper.LastError(tasks));
    }
}
