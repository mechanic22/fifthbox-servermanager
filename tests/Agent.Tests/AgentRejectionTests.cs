using FifthBox.ServerManager.Shared.Agents;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Agent.Tests;

[TestClass]
public class AgentRejectionTests
{
    [TestMethod]
    public void The_hosts_rejection_is_recognised_by_the_agent()
    {
        // Both ends of this contract are in different projects, so the round-trip is the thing worth
        // asserting — a reworded message on one side must not silently stop the other side reacting.
        var thrown = new HubException(AgentRejection.Message("this agent is not enrolled here."));

        Assert.IsTrue(AgentRejection.IsRejection(thrown));
    }

    [TestMethod]
    public void An_ordinary_disconnect_is_not_a_rejection()
    {
        // The agent must keep reconnecting through these; only a refused credential is terminal.
        Assert.IsFalse(AgentRejection.IsRejection(new IOException("The remote party closed the connection.")));
        Assert.IsFalse(AgentRejection.IsRejection(new TaskCanceledException("A task was canceled.")));
    }

    [TestMethod]
    public void A_clean_close_carries_no_error_and_is_not_a_rejection()
    {
        Assert.IsFalse(AgentRejection.IsRejection(null));
    }

    [TestMethod]
    public void The_detail_survives_so_the_operator_is_told_what_to_do()
    {
        var message = AgentRejection.Message("delete agent-credentials.json");

        StringAssert.Contains(message, "delete agent-credentials.json");
    }
}
