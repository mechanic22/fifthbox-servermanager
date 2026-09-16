using System.Globalization;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// splits off the RFC3339 stamp docker prefixes when Timestamps=true
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
