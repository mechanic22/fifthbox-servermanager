namespace FifthBox.ServerManager.Agent;

public enum StopStep
{
    /// Write the workload's configured stop command to its stdin.
    ConsoleCommand,

    /// SIGTERM on Unix, close-the-window on Windows.
    Signal,

    Kill,
}

/// Decides how to ask a process to go away, in order of politeness.
public static class StopLadder
{
    /// A step that could not do anything is left out rather than attempted and ignored, because each
    /// attempt that runs is allowed to spend the whole stop grace waiting.
    ///
    /// Signal always stays in: on Unix it's a real SIGTERM, and on Windows it fails immediately without
    /// consuming any of the budget, which is exactly why a headless workload needs a stop command to
    /// have any polite option at all.
    public static IReadOnlyList<StopStep> For(string? stopCommand, bool hasConsole)
    {
        var steps = new List<StopStep>(3);

        if (hasConsole && !string.IsNullOrWhiteSpace(stopCommand))
        {
            steps.Add(StopStep.ConsoleCommand);
        }

        steps.Add(StopStep.Signal);
        steps.Add(StopStep.Kill);
        return steps;
    }
}
