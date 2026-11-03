using FifthBox.ServerManager.Client.Web.Components;
using FifthBox.ServerManager.Shared.Certificates;
using FifthBox.ServerManager.Shared.Cluster;
using FifthBox.ServerManager.Shared.Nodes;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class StatusTonesTests
{
    private static WorkloadRuntimeStatus Status(WorkloadState state, string? lastError = null) =>
        new() { Name = "app", State = state, LastError = lastError };

    private static WorkloadTask Task(WorkloadTaskState state, WorkloadTaskState desired) =>
        new() { Id = "t", State = state, DesiredState = desired };

    [TestMethod]
    [DataRow(WorkloadState.Running, StatusTone.Good)]
    [DataRow(WorkloadState.Partial, StatusTone.Warn)]
    [DataRow(WorkloadState.Updating, StatusTone.Warn)]
    [DataRow(WorkloadState.Stopped, StatusTone.Neutral)]
    [DataRow(WorkloadState.NotDeployed, StatusTone.Neutral)]
    public void Workload_state_maps_to_one_tone_everywhere(WorkloadState state, StatusTone expected) =>
        Assert.AreEqual(expected, StatusTones.For(state));

    [TestMethod]
    public void A_missing_service_is_bad_whatever_state_it_reports() =>
        Assert.AreEqual(StatusTone.Bad, StatusTones.For(Status(WorkloadState.Running), missing: true));

    [TestMethod]
    public void A_last_error_turns_a_struggling_workload_bad()
    {
        Assert.AreEqual(StatusTone.Bad, StatusTones.For(Status(WorkloadState.Partial, "no suitable node")));
        Assert.AreEqual(StatusTone.Bad, StatusTones.For(Status(WorkloadState.Stopped, "no suitable node")));
    }

    [TestMethod]
    public void A_running_workload_stays_good_with_an_old_error_on_it() =>
        Assert.AreEqual(StatusTone.Good, StatusTones.For(Status(WorkloadState.Running, "earlier failure")));

    [TestMethod]
    public void An_empty_last_error_falls_through_to_the_state() =>
        Assert.AreEqual(StatusTone.Warn, StatusTones.For(Status(WorkloadState.Partial, string.Empty)));

    [TestMethod]
    public void A_task_on_its_way_out_is_neutral_even_if_it_failed() =>
        Assert.AreEqual(StatusTone.Neutral, StatusTones.For(Task(WorkloadTaskState.Failed, WorkloadTaskState.Shutdown)));

    [TestMethod]
    public void A_task_that_should_be_running_is_judged_by_its_state()
    {
        Assert.AreEqual(StatusTone.Good, StatusTones.For(Task(WorkloadTaskState.Running, WorkloadTaskState.Running)));
        Assert.AreEqual(StatusTone.Bad, StatusTones.For(Task(WorkloadTaskState.Failed, WorkloadTaskState.Running)));
        Assert.AreEqual(StatusTone.Warn, StatusTones.For(Task(WorkloadTaskState.Starting, WorkloadTaskState.Running)));
    }

    [TestMethod]
    [DataRow(NodeStatus.Ready, StatusTone.Good)]
    [DataRow(NodeStatus.Down, StatusTone.Bad)]
    [DataRow(NodeStatus.Disconnected, StatusTone.Warn)]
    [DataRow(NodeStatus.Unknown, StatusTone.Neutral)]
    public void Node_status(NodeStatus status, StatusTone expected) =>
        Assert.AreEqual(expected, StatusTones.For(status));

    [TestMethod]
    [DataRow(SwarmMembership.Active, StatusTone.Good)]
    [DataRow(SwarmMembership.Error, StatusTone.Bad)]
    [DataRow(SwarmMembership.Locked, StatusTone.Bad)]
    [DataRow(SwarmMembership.Pending, StatusTone.Warn)]
    [DataRow(SwarmMembership.Inactive, StatusTone.Neutral)]
    public void Swarm_membership(SwarmMembership membership, StatusTone expected) =>
        Assert.AreEqual(expected, StatusTones.For(membership));

    [TestMethod]
    public void Certificate_status()
    {
        Assert.AreEqual(StatusTone.Good, StatusTones.For(CertificateStatus.Valid));
        Assert.AreEqual(StatusTone.Warn, StatusTones.For(CertificateStatus.Pending));
        Assert.AreEqual(StatusTone.Bad, StatusTones.For(CertificateStatus.Failed));
        Assert.AreEqual(StatusTone.Neutral, StatusTones.For((CertificateStatus?)null));
    }
}
