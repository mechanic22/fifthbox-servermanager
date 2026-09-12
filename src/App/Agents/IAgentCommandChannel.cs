using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Agents;

/// Sends commands to a connected agent and awaits its reply. Implemented in the Host (over the AgentHub
/// connection); consumed by the AgentBackend. The App/integration never touch the hub directly.
public interface IAgentCommandChannel
{
    bool IsConnected(string agentId);

    Task<AgentWorkloadStatus> DeployAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default);
    Task<AgentWorkloadStatus> StopAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task<AgentWorkloadStatus> GetStatusAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task<IReadOnlyList<WorkloadLogLine>> GetLogsAsync(string agentId, string workloadName, int tail, CancellationToken ct = default);

    /// Write a line to a running workload's stdin. The reply is its status, so a caller can tell whether
    /// there was anything listening.
    Task<AgentWorkloadStatus> SendConsoleAsync(string agentId, string workloadName, string text, CancellationToken ct = default);

    /// Start acquiring a workload's files. The agent replies as soon as it has started, not when done.
    Task<AgentWorkloadStatus> UpdateAsync(string agentId, AgentWorkloadSpec spec, CancellationToken ct = default);

    /// Browse and edit the files under a managed workload's own directory. The agent resolves every path
    /// against that directory and refuses anything outside it.
    Task<IReadOnlyList<WorkloadFileEntry>> ListFilesAsync(string agentId, string workloadName, string path, CancellationToken ct = default);
    Task<WorkloadFileContent> ReadFileAsync(string agentId, string workloadName, string path, CancellationToken ct = default);
    Task WriteFileAsync(string agentId, string workloadName, string path, string text, CancellationToken ct = default);

    /// Ask an agent to start (or stop) streaming a workload's output as it is produced.
    Task StartFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default);
    Task StopFollowingLogsAsync(string agentId, string workloadName, CancellationToken ct = default);
}
