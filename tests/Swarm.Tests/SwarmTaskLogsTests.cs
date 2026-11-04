using System.Buffers.Binary;
using System.Text;
using FifthBox.ServerManager.Integrations.Swarm;

namespace FifthBox.ServerManager.Integrations.Swarm.Tests;

[TestClass]
public class SwarmTaskLogsTests
{
    private static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = stream;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(frame, 8);
        return frame;
    }

    private static Task<(string Stdout, string Stderr)> Read(byte[] bytes, bool tty = false, int max = 1024) =>
        DockerLogFrames.ReadAsync(new MemoryStream(bytes), tty, max, CancellationToken.None);

    [TestMethod]
    public async Task Frames_are_split_into_stdout_and_stderr()
    {
        var (stdout, stderr) = await Read([.. Frame(1, "out one\n"), .. Frame(2, "err\n"), .. Frame(1, "out two\n")]);

        Assert.AreEqual("out one\nout two\n", stdout);
        Assert.AreEqual("err\n", stderr);
    }

    [TestMethod]
    public async Task A_tty_stream_has_no_frames() =>
        Assert.AreEqual("plain\n", (await Read(Encoding.UTF8.GetBytes("plain\n"), tty: true)).Stdout);

    [TestMethod]
    public async Task An_empty_stream_is_just_empty() =>
        Assert.AreEqual((string.Empty, string.Empty), await Read([]));

    [TestMethod]
    public async Task A_truncated_frame_keeps_what_arrived()
    {
        var frame = Frame(1, "hello world\n");

        Assert.AreEqual("hello", (await Read(frame[..13])).Stdout);
    }

    [TestMethod]
    public async Task Reading_stops_at_the_byte_cap()
    {
        var (stdout, _) = await Read([.. Frame(1, "0123456789"), .. Frame(1, "abcdef")], max: 4);

        Assert.AreEqual("0123", stdout);
    }

    // FBSM_TEST_DOCKER=unix:///path/docker.sock FBSM_TEST_TASK=<running task id> dotnet test --filter TestCategory=Integration
    [TestMethod]
    [TestCategory("Integration")]
    public async Task Reads_a_real_tasks_logs()
    {
        var endpoint = Environment.GetEnvironmentVariable("FBSM_TEST_DOCKER");
        var task = Environment.GetEnvironmentVariable("FBSM_TEST_TASK");
        if (endpoint is null || task is null)
        {
            Assert.Inconclusive("Needs FBSM_TEST_DOCKER and FBSM_TEST_TASK.");
        }

        using var logs = new SwarmTaskLogs(new Uri(endpoint));

        var (stdout, _) = await logs.ReadAsync(task, tty: false, tail: 5, CancellationToken.None);

        Assert.IsNotEmpty(SwarmLogParser.Parse(stdout, FifthBox.ServerManager.Shared.Workloads.LogStream.Stdout).ToList());
    }
}
