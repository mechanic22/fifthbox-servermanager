using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Swarm.Tests;

[TestClass]
public class SwarmLogParserTests
{
    [TestMethod]
    public void Splits_the_rfc3339_stamp_off_the_front_of_each_line()
    {
        var lines = SwarmLogParser.Parse("2026-08-05T10:11:12.5Z starting up\n", LogStream.Stdout).ToList();

        Assert.AreEqual("starting up", lines.Single().Text);
        Assert.AreEqual(2026, lines.Single().Timestamp!.Value.Year);
    }

    [TestMethod]
    public void Keeps_the_whole_line_when_there_is_no_timestamp()
    {
        var lines = SwarmLogParser.Parse("no stamp here\n", LogStream.Stdout).ToList();

        Assert.AreEqual("no stamp here", lines.Single().Text);
        Assert.IsNull(lines.Single().Timestamp);
    }

    [TestMethod]
    public void A_line_starting_with_a_word_is_not_mistaken_for_a_stamp()
    {
        var lines = SwarmLogParser.Parse("ERROR something broke\n", LogStream.Stderr).ToList();

        Assert.AreEqual("ERROR something broke", lines.Single().Text);
        Assert.IsNull(lines.Single().Timestamp);
    }

    [TestMethod]
    public void Carries_the_stream_it_was_read_from()
    {
        var lines = SwarmLogParser.Parse("boom\n", LogStream.Stderr).ToList();

        Assert.AreEqual(LogStream.Stderr, lines.Single().Stream);
    }

    [TestMethod]
    public void Blank_lines_and_carriage_returns_are_dropped()
    {
        var lines = SwarmLogParser.Parse("one\r\n\n\ntwo\r\n", LogStream.Stdout).ToList();

        CollectionAssert.AreEqual(new[] { "one", "two" }, lines.Select(l => l.Text).ToArray());
    }

    [TestMethod]
    public void Empty_payload_yields_nothing()
    {
        Assert.IsEmpty(SwarmLogParser.Parse(string.Empty, LogStream.Stdout).ToList());
    }

    [TestMethod]
    public void Lines_from_a_task_that_is_no_longer_running_come_back_previous()
    {
        var payload =
            "2026-08-05T10:00:00Z com.docker.swarm.node.id=n1,com.docker.swarm.service.id=s1,com.docker.swarm.task.id=old crashed\n" +
            "2026-08-05T10:01:00Z com.docker.swarm.node.id=n1,com.docker.swarm.service.id=s1,com.docker.swarm.task.id=new started\n";

        var lines = SwarmLogParser.Parse(payload, LogStream.Stdout, new HashSet<string> { "new" }).ToList();

        CollectionAssert.AreEqual(new[] { "crashed", "started" }, lines.Select(l => l.Text).ToArray());
        CollectionAssert.AreEqual(new[] { true, false }, lines.Select(l => l.Previous).ToArray());
    }

    [TestMethod]
    public void With_nothing_running_no_line_is_previous()
    {
        var lines = SwarmLogParser.Parse(
            "2026-08-05T10:00:00Z com.docker.swarm.task.id=old crashed\n", LogStream.Stdout, new HashSet<string>()).ToList();

        Assert.AreEqual("crashed", lines.Single().Text);
        Assert.IsFalse(lines.Single().Previous);
    }

    [TestMethod]
    public void Empty_details_still_strip_cleanly()
    {
        var lines = SwarmLogParser.Parse("2026-08-05T10:00:00Z  hello world\n", LogStream.Stdout, new HashSet<string> { "t" }).ToList();

        Assert.AreEqual("hello world", lines.Single().Text);
        Assert.IsFalse(lines.Single().Previous);
    }

    [TestMethod]
    public void A_message_word_is_not_mistaken_for_details()
    {
        var lines = SwarmLogParser.Parse("2026-08-05T10:00:00Z listening on 8080\n", LogStream.Stdout, new HashSet<string> { "t" }).ToList();

        Assert.AreEqual("listening on 8080", lines.Single().Text);
    }
}
