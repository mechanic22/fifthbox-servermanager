namespace FifthBox.ServerManager.Agent;

public static class AgentPaths
{
    /// relative to the exe, a windows service runs in System32 and would re-enroll on every start
    public static string Resolve(string path, string baseDirectory) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path);
}

public sealed class AgentOptions
{
    public string HostUrl { get; set; } = "http://localhost:5080";

    /// only used for the first enroll, the stored credential takes over after
    public string EnrollmentKey { get; set; } = string.Empty;

    /// empty means machine name
    public string Name { get; set; } = string.Empty;

    public string CredentialsPath { get; set; } = "agent-credentials.json";

    public string StatePath { get; set; } = "agent-state.json";

    public string RootPath { get; set; } = "workloads";

    public int HeartbeatSeconds { get; set; } = 30;

    public string ResolvedCredentialsPath => AgentPaths.Resolve(CredentialsPath, AppContext.BaseDirectory);

    public string ResolvedStatePath => AgentPaths.Resolve(StatePath, AppContext.BaseDirectory);

    public string ResolvedRootPath => AgentPaths.Resolve(RootPath, AppContext.BaseDirectory);
}
