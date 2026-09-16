using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Shared.Agents;

public record AgentWorkloadSpec
{
    public required string Name { get; init; }
    public string Command { get; init; } = string.Empty;
    public IReadOnlyList<string> Args { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<EnvVar> Env { get; init; } = [];

    /// declarative, the workload binds these not the agent. used for collision checks
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];

    public RestartPolicy RestartPolicy { get; init; } = RestartPolicy.OnFailure;

    public int StopGraceSeconds { get; init; } = 10;

    /// written to stdin to stop politely (a game server's "quit"). on windows without it a stop is always a kill
    public string? StopCommand { get; init; }

    /// agent owns <AgentRoot>/<name>/, runs there and resolves a relative Command against it. WorkingDirectory is ignored
    public bool ManagedDirectory { get; init; }

    /// acquire runs on its own command, never during a deploy
    public WorkloadSourceSpec? Source { get; init; }
}
