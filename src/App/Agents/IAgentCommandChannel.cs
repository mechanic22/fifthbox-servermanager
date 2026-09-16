using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

public interface IAgentCommandChannel
{
    bool IsConnected(string agentId);

    Task<AgentWorkloadStatus> DeployAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default);
    Task<AgentWorkloadStatus> StopAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task<AgentWorkloadStatus> GetStatusAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(string agentId, string workloadName, int tail, CancellationToken ct = default);

    /// replies with status so you can tell if anything was listening
    Task<AgentWorkloadStatus> SendConsoleAsync(string agentId, string workloadName, string text, CancellationToken ct = default);

    /// replies once started, not when done
    Task<AgentWorkloadStatus> UpdateAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default);

    /// paths resolve against the workload dir, the agent refuses anything outside it
    Task<IReadOnlyList<WorkloadFileEntry>> ListFilesAsync(string agentId, string workloadName, string path, CancellationToken ct = default);
    Task<WorkloadFileContent> ReadFileAsync(string agentId, string workloadName, string path, CancellationToken ct = default);
    Task WriteFileAsync(string agentId, string workloadName, string path, string text, CancellationToken ct = default);

    Task StartFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task StopFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default);
}
