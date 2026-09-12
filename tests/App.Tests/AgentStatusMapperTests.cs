using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Tests;

[TestClass]
public class AgentStatusMapperTests
{
    [TestMethod]
    public void A_running_process_is_one_replica_of_one()
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-5);

        var status = AgentStatusMapper.ToRuntimeStatus("srcds", new AgentWorkloadStatus
        {
            Name = "srcds",
            Running = true,
            Pid = 4242,
            StartedAt = started,
            RestartCount = 2,
            Adopted = true,
        });

        Assert.AreEqual("srcds", status.Name);
        Assert.IsTrue(status.Deployed);
        Assert.AreEqual(WorkloadState.Running, status.State);
        Assert.AreEqual(1, status.DesiredReplicas);
        Assert.AreEqual(1, status.RunningReplicas);
        Assert.AreEqual(4242, status.Pid);
        Assert.AreEqual(started, status.StartedAt);
        Assert.AreEqual(2, status.RestartCount);
        Assert.IsTrue(status.Adopted);
    }

    [TestMethod]
    public void A_stopped_process_keeps_why_it_stopped()
    {
        var status = AgentStatusMapper.ToRuntimeStatus("srcds", new AgentWorkloadStatus
        {
            Name = "srcds",
            Running = false,
            ExitCode = 137,
            Detail = "killed",
        });

        Assert.IsFalse(status.Deployed);
        Assert.AreEqual(WorkloadState.Stopped, status.State);
        Assert.AreEqual(0, status.RunningReplicas);
        Assert.AreEqual(137, status.ExitCode);
        Assert.AreEqual("killed", status.Detail);
    }

    [TestMethod]
    public void The_name_comes_from_the_caller_not_the_report()
    {
        // The backend knows the workload it asked about; the agent's copy is only a label.
        var status = AgentStatusMapper.ToRuntimeStatus("expected", new AgentWorkloadStatus { Name = "whatever", Running = true });

        Assert.AreEqual("expected", status.Name);
    }

    [TestMethod]
    public void An_acquire_in_flight_reads_as_updating()
    {
        var status = AgentStatusMapper.ToRuntimeStatus("srcds", new AgentWorkloadStatus
        {
            Name = "srcds", Running = false, Updating = true,
        });

        Assert.AreEqual(WorkloadState.Updating, status.State);
        Assert.IsTrue(status.Updating);
    }

    [TestMethod]
    public void Updating_wins_over_running_so_a_stale_process_flag_cannot_hide_it()
    {
        var status = AgentStatusMapper.ToRuntimeStatus("srcds", new AgentWorkloadStatus
        {
            Name = "srcds", Running = true, Updating = true,
        });

        Assert.AreEqual(WorkloadState.Updating, status.State);
    }

    [TestMethod]
    public void The_installed_version_reaches_the_runtime_status()
    {
        var status = AgentStatusMapper.ToRuntimeStatus("srcds", new AgentWorkloadStatus
        {
            Name = "srcds", Running = true, InstalledVersion = "server.zip (1024 bytes)",
        });

        Assert.AreEqual("server.zip (1024 bytes)", status.InstalledVersion);
    }
}
