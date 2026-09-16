using System.Text;
using FifthBox.ServerManager.Integrations.Swarm;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

/// docker reads end wherever the socket fills, so lines and chars can split across reads
[TestClass]
public class SwarmLogBufferTests
{
    private static IReadOnlyList<WorkloadLogLine> Feed(SwarmLogBuffer buffer, string chunk, LogStream stream = LogStream.Stdout)
    {
        var bytes = Encoding.UTF8.GetBytes(chunk);
        return buffer.Feed(bytes, bytes.Length, stream);
    }

    [TestMethod]
    public void A_complete_line_comes_straight_out()
    {
        var lines = Feed(new SwarmLogBuffer(), "hello\n");

        Assert.HasCount(1, lines);
        Assert.AreEqual("hello", lines[0].Text);
    }

    [TestMethod]
    public void An_unterminated_line_is_held_back()
        => Assert.IsEmpty(Feed(new SwarmLogBuffer(), "no newline yet"));

    [TestMethod]
    public void A_line_split_across_two_reads_is_rejoined()
    {
        var buffer = new SwarmLogBuffer();

        Assert.IsEmpty(Feed(buffer, "first half "));
        var lines = Feed(buffer, "second half\n");

        Assert.HasCount(1, lines);
        Assert.AreEqual("first half second half", lines[0].Text);
    }

    [TestMethod]
    public void Several_lines_in_one_read_all_come_out()
    {
        var lines = Feed(new SwarmLogBuffer(), "one\ntwo\nthree\n");

        Assert.HasCount(3, lines);
        Assert.AreEqual("one", lines[0].Text);
        Assert.AreEqual("three", lines[2].Text);
    }

    [TestMethod]
    public void A_trailing_partial_line_waits_for_the_next_read()
    {
        var buffer = new SwarmLogBuffer();

        var first = Feed(buffer, "done\nnot done");
        Assert.HasCount(1, first);
        Assert.AreEqual("done", first[0].Text);

        var second = Feed(buffer, " now\n");
        Assert.HasCount(1, second);
        Assert.AreEqual("not done now", second[0].Text);
    }

    [TestMethod]
    public void A_multi_byte_character_split_across_reads_survives()
    {
        var buffer = new SwarmLogBuffer();
        var bytes = Encoding.UTF8.GetBytes("café\n");

        Assert.IsEmpty(buffer.Feed(bytes, 4, LogStream.Stdout));      // "caf" + first byte of é
        var lines = buffer.Feed(bytes[4..], bytes.Length - 4, LogStream.Stdout);

        Assert.HasCount(1, lines);
        Assert.AreEqual("café", lines[0].Text);
    }

    [TestMethod]
    public void The_stream_is_carried_through()
    {
        var lines = Feed(new SwarmLogBuffer(), "boom\n", LogStream.Stderr);

        Assert.AreEqual(LogStream.Stderr, lines[0].Stream);
    }

    [TestMethod]
    public void A_docker_timestamp_prefix_is_split_off()
    {
        var lines = Feed(new SwarmLogBuffer(), "2026-08-17T09:15:00.000000000Z started\n");

        Assert.HasCount(1, lines);
        Assert.AreEqual("started", lines[0].Text);
        Assert.IsNotNull(lines[0].Timestamp);
    }

    [TestMethod]
    public void An_empty_read_yields_nothing()
        => Assert.IsEmpty(new SwarmLogBuffer().Feed([], 0, LogStream.Stdout));

    [TestMethod]
    public void Output_past_the_old_two_hundred_line_tail_keeps_flowing()
    {
        // regression: poller diffed by count against a tail of 200, so after 200 lines following stopped forever
        var buffer = new SwarmLogBuffer();
        var total = 0;

        for (var i = 0; i < 500; i++)
        {
            total += Feed(buffer, $"line {i}\n").Count;
        }

        Assert.AreEqual(500, total);
    }
}
