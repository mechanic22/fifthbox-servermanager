using System.Globalization;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Turns a docker log payload into lines. With Timestamps=true docker prefixes each line with an
/// RFC3339 stamp and a space; it is split off so the UI isn't showing raw text with a timestamp glued
/// to the front. Pure so the framing rules are testable without a daemon.
public static class SwarmLogParser
{
    public static IEnumerable<WorkloadLogLine> Parse(string payload, LogStream stream)
    {
        if (string.IsNullOrEmpty(payload))
        {
            yield break;
        }

        foreach (var raw in payload.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var split = line.IndexOf(' ');
            if (split > 0 && DateTimeOffset.TryParse(line[..split], CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var timestamp))
            {
                yield return new WorkloadLogLine { Text = line[(split + 1)..], Stream = stream, Timestamp = timestamp };
            }
            else
            {
                yield return new WorkloadLogLine { Text = line, Stream = stream };
            }
        }
    }
}
