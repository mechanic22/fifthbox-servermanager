using System.Text;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Reassembles docker's log reads into whole lines. A read boundary lands wherever the socket happens to
/// fill, so it can split a line in half and even split a multi-byte character in half — both are held
/// back until the rest arrives. One buffer per stream, since stdout and stderr interleave.
public sealed class SwarmLogBuffer
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _pending = new();

    public IReadOnlyList<WorkloadLogLine> Feed(byte[] buffer, int count, LogStream stream)
    {
        if (count <= 0)
        {
            return [];
        }

        var chars = new char[Encoding.UTF8.GetMaxCharCount(count)];
        _pending.Append(chars, 0, _decoder.GetChars(buffer, 0, count, chars, 0));

        var text = _pending.ToString();
        var lastBreak = text.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            return [];
        }

        _pending.Clear();
        _pending.Append(text[(lastBreak + 1)..]);
        return [.. SwarmLogParser.Parse(text[..(lastBreak + 1)], stream)];
    }
}
