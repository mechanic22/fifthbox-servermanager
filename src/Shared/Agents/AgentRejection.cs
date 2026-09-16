namespace FifthBox.ServerManager.Shared.Agents;

/// terminal, the same credential never works so the agent stops instead of reconnecting forever
/// matched on a stable prefix so we're not guessing at exception messages
public static class AgentRejection
{
    public const string Prefix = "agent-credential-rejected:";

    public static string Message(string detail) => $"{Prefix} {detail}";

    public static bool IsRejection(Exception? error) =>
        error?.Message.Contains(Prefix, StringComparison.Ordinal) == true;
}
