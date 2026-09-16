namespace FifthBox.ServerManager.Agent;

public enum StopStep
{
    ConsoleCommand,

    /// SIGTERM on unix, close-the-window on windows
    Signal,

    Kill,
}

public static class StopLadder
{
    /// useless steps are left out since each one can burn the full grace
    /// Signal always stays, it fails instantly on windows so headless ones there need a stop command
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
