namespace FifthBox.ServerManager.Agent;

public static class AgentPaths
{
    /// Anchors a relative path to the executable's own folder. As a Windows service the working
    /// directory is C:\Windows\System32, so a relative credential path would be written there — or fail,
    /// which is worse: the agent then re-enrolls on every start and each restart leaves another dead
    /// entry in the Agents list. Absolute paths are the operator's choice and pass through.
    public static string Resolve(string path, string baseDirectory) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path);
}

public sealed class AgentOptions
{
    public string HostUrl { get; set; } = "http://localhost:5080";

    /// One-time-use on first run: presented to enroll. After enrollment the stored credential is used.
    public string EnrollmentKey { get; set; } = string.Empty;

    /// Defaults to the machine name when empty.
    public string Name { get; set; } = string.Empty;

    public string CredentialsPath { get; set; } = "agent-credentials.json";

    /// Process table for re-attaching to workloads that outlived the agent.
    public string StatePath { get; set; } = "agent-state.json";

    /// Where the agent creates a directory per managed workload. Relative paths anchor to the exe, same
    /// as the credential and state files.
    public string RootPath { get; set; } = "workloads";

    public int HeartbeatSeconds { get; set; } = 30;

    public string ResolvedCredentialsPath => AgentPaths.Resolve(CredentialsPath, AppContext.BaseDirectory);

    public string ResolvedStatePath => AgentPaths.Resolve(StatePath, AppContext.BaseDirectory);

    public string ResolvedRootPath => AgentPaths.Resolve(RootPath, AppContext.BaseDirectory);
}
