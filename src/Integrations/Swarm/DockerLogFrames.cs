using System.Buffers.Binary;
using System.Text;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// without a tty docker wraps output in frames: stream byte, 3 zeros, big-endian length
public static class DockerLogFrames
{
    private const byte StderrStream = 2;

    /// stops at maxBytes, a tail of a few lines can still be enormous if they have no line breaks
    public static async Task<(string Stdout, string Stderr)> ReadAsync(Stream stream, bool tty, int maxBytes, CancellationToken ct)
    {
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();

        if (tty)
        {
            await CopyAsync(stream, stdout, maxBytes, ct);
            return (Encoding.UTF8.GetString(stdout.ToArray()), string.Empty);
        }

        var header = new byte[8];
        var remaining = maxBytes;
        while (remaining > 0 && await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct) == header.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
            var take = (int)Math.Min(length, (uint)remaining);
            await CopyAsync(stream, header[0] == StderrStream ? stderr : stdout, take, ct);
            remaining -= take;

            // cut short on the cap, the rest of this frame would read as a header
            if (take < length)
            {
                break;
            }
        }

        return (Encoding.UTF8.GetString(stdout.ToArray()), Encoding.UTF8.GetString(stderr.ToArray()));
    }

    private static async Task CopyAsync(Stream from, Stream to, int count, CancellationToken ct)
    {
        var buffer = new byte[Math.Min(count, 64 * 1024)];
        while (count > 0)
        {
            var read = await from.ReadAsync(buffer.AsMemory(0, Math.Min(count, buffer.Length)), ct);
            if (read == 0)
            {
                return;
            }

            await to.WriteAsync(buffer.AsMemory(0, read), ct);
            count -= read;
        }
    }
}
