using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.App.Agents;

public static class AgentWorkloadSpecMapper
{
    public static AgentWorkloadSpec ToSpec(WorkloadDeployment d) => new()
    {
        Name = d.Name,
        Command = d.Command ?? string.Empty,
        Args = d.Args,
        WorkingDirectory = d.WorkingDirectory,
        Env = d.Env,
        Ports = d.Ports,
        RestartPolicy = d.RestartPolicy,
        StopGraceSeconds = d.StopGraceSeconds,
        StopCommand = d.StopCommand,
        ManagedDirectory = d.ManagedDirectory,
        Source = d.Source,
    };
}
