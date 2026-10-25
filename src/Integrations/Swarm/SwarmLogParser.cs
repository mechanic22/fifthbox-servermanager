using System.Globalization;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// splits off the RFC3339 stamp docker prefixes when Timestamps=true
public static class SwarmLogParser
{
    private const string TaskLabel = "com.docker.swarm.task.id=";

    /// pass currentTasks when the logs were fetched with Details, lines from any other task come back Previous
    public static IEnumerable<WorkloadLogLine> Parse(string payload, LogStream stream, IReadOnlySet<string>? currentTasks = null)
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
                var (text, previous) = currentTasks is null ? (line[(split + 1)..], false) : SplitDetails(line[(split + 1)..], currentTasks);
                yield return new WorkloadLogLine { Text = text, Stream = stream, Timestamp = timestamp, Previous = previous };
            }
            else
            {
                var (text, previous) = currentTasks is null ? (line, false) : SplitDetails(line, currentTasks);
                yield return new WorkloadLogLine { Text = text, Stream = stream, Previous = previous };
            }
        }
    }

    // details are "k=v,k=v " ahead of the message, empty when docker had no labels
    private static (string Text, bool Previous) SplitDetails(string rest, IReadOnlySet<string> currentTasks)
    {
        var space = rest.IndexOf(' ');
        var details = space < 0 ? rest : rest[..space];
        if (details.Length > 0 && !details.Contains('='))
        {
            return (rest, false);
        }

        var text = space < 0 ? "" : rest[(space + 1)..];
        var task = details.Split(',').FirstOrDefault(d => d.StartsWith(TaskLabel, StringComparison.Ordinal))?[TaskLabel.Length..];

        // nothing running to compare against, so nothing is "previous"
        var previous = task is not null && currentTasks.Count > 0 && !currentTasks.Contains(task);
        return (text, previous);
    }
}
