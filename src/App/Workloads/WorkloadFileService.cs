using FifthBox.ServerManager.App.Access;
using FifthBox.ServerManager.App.Agents;
using FifthBox.ServerManager.Shared.Access;
using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public interface IWorkloadFileService
{
    Task<IReadOnlyList<WorkloadFileEntry>> ListAsync(Caller caller, string id, string? path, CancellationToken ct = default);
    Task<WorkloadFileContent> ReadAsync(Caller caller, string id, string path, CancellationToken ct = default);
    Task WriteAsync(Caller caller, string id, string path, string text, CancellationToken ct = default);
}

/// not on IWorkloadBackend, container files live in a volume we can't reach from here
/// needs Configure not Operate, game server configs hold passwords
public sealed class WorkloadFileService(WorkloadLoader loader, IAgentCommandChannel agents) : IWorkloadFileService
{
    public async Task<IReadOnlyList<WorkloadFileEntry>> ListAsync(Caller caller, string id, string? path, CancellationToken ct = default)
    {
        var (workload, agentId) = await ManagedAsync(caller, id, ct);
        return await agents.ListFilesAsync(agentId, workload.Name, path ?? string.Empty, ct);
    }

    public async Task<WorkloadFileContent> ReadAsync(Caller caller, string id, string path, CancellationToken ct = default)
    {
        var (workload, agentId) = await ManagedAsync(caller, id, ct);
        RequirePath(path);
        return await agents.ReadFileAsync(agentId, workload.Name, path, ct);
    }

    public async Task WriteAsync(Caller caller, string id, string path, string text, CancellationToken ct = default)
    {
        var (workload, agentId) = await ManagedAsync(caller, id, ct);
        RequirePath(path);
        await agents.WriteFileAsync(agentId, workload.Name, path, text, ct);
    }

    private async Task<(Workload Workload, string AgentId)> ManagedAsync(Caller caller, string id, CancellationToken ct)
    {
        var (workload, _) = await loader.LoadAsync(caller, id, AccessLevel.Configure, ct);

        if (workload.Kind != WorkloadKind.Native)
        {
            throw new ConflictException("Only workloads that run on an agent have files here.");
        }

        if (!workload.ManagedDirectory)
        {
            throw new ConflictException($"'{workload.Name}' points at a directory somebody else owns, so the agent won't browse it.");
        }

        if (workload.AgentId is not { Length: > 0 } agentId)
        {
            throw new ConflictException($"'{workload.Name}' has no agent assigned.");
        }

        if (!agents.IsConnected(agentId))
        {
            throw new ConflictException("That agent is offline.");
        }

        return (workload, agentId);
    }

    private static void RequirePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ValidationException(nameof(WriteWorkloadFileRequest.Path), "Name the file.");
        }
    }
}
