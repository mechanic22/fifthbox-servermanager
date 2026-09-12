using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Agents;

/// A native workload the Host tells an agent to run.
public record AgentWorkloadSpec
{
    public required string Name { get; init; }
    public string Command { get; init; } = string.Empty;
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<EnvVar> Env { get; init; } = [];

    /// Ports the process is expected to listen on. Declarative — the agent doesn't bind these, the
    /// workload does. They exist so the platform can detect collisions and, later, report reachability.
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];

    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.OnFailure;

    /// How long to wait after asking the process to terminate before killing it.
    public int StopGraceSeconds { get; init; } = 10;

    /// What to write to the process's stdin to ask it to shut down (a game server's "stop"/"quit"). On
    /// Windows this is the only polite option a headless process has — there is no window to close and
    /// no SIGTERM — so without it a stop is always a kill.
    public string? StopCommand { get; init; }

    /// The agent owns this workload's directory: it creates <AgentRoot>/<name>/, runs the process there,
    /// and resolves a relative Command against it. WorkingDirectory is ignored — the install root is it.
    public bool ManagedDirectory { get; init; }

    /// Where the workload's files come from. Acquiring runs on its own command, never as part of a
    /// deploy.
    public WorkloadSourceSpec? Source { get; init; }
}
