namespace FifthBox.ServerManager.Shared.Agents;

/// How the Host tells an agent its credential is no good, and how the agent recognises that answer.
///
/// A rejection is terminal in a way an ordinary disconnect isn't: retrying with the same credential can
/// never succeed, so the agent has to stop rather than reconnect forever. Matching on a stable prefix
/// keeps that decision out of exception-message guesswork.
public static class AgentRejection
{
    public const string Prefix = "agent-credential-rejected:";

    public static string Message(string detail) => $"{Prefix} {detail}";

    public static bool IsRejection(Exception? error) =>
        error?.Message.Contains(Prefix, StringComparison.Ordinal) == true;
}
