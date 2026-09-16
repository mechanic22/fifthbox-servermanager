using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Exceptions;

namespace FifthBox.ServerManager.App.Agents;

public interface IAgentService
{
    Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct = default);
    Task<EnrollAgentResponse> EnrollAsync(EnrollAgentRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// replaces any existing key, plaintext comes back once
    Task<EnrollmentKeyResponse> GenerateEnrollmentKeyAsync(CancellationToken ct = default);

    Task<bool> AuthenticateAsync(string agentId, string secret, CancellationToken ct = default);

    Task MarkSeenAsync(string agentId, CancellationToken ct = default);
}

public sealed class AgentService(
    IAgentRepository agents,
    IEnrollmentKeyStore enrollmentKeys,
    IAgentRegistry registry,
    TimeProvider clock) : IAgentService
{
    public async Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct = default)
        => (await agents.ListAsync(ct)).Select(Map).ToList();

    public async Task<EnrollAgentResponse> EnrollAsync(EnrollAgentRequest request, CancellationToken ct = default)
    {
        var keyHash = await enrollmentKeys.GetHashAsync(ct);
        if (!AgentSecrets.Verify(request.EnrollmentKey ?? string.Empty, keyHash))
        {
            // same response for a wrong or missing key, don't leak "no key configured"
            throw new UnauthorizedException("Enrollment failed.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? "agent" : request.Name.Trim();
        var secret = AgentSecrets.Generate();
        var agent = new Agent
        {
            Name = name,
            Platform = request.Platform,
            SecretHash = AgentSecrets.Hash(secret),
            EnrolledAt = clock.GetUtcNow(),
        };

        await agents.AddAsync(agent, ct);
        return new EnrollAgentResponse { AgentId = agent.Id, Secret = secret };
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var agent = await agents.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Agent '{id}' not found.");
        await agents.RemoveAsync(agent, ct);
    }

    public async Task<EnrollmentKeyResponse> GenerateEnrollmentKeyAsync(CancellationToken ct = default)
    {
        var key = AgentSecrets.Generate();
        await enrollmentKeys.SetHashAsync(AgentSecrets.Hash(key), ct);
        return new EnrollmentKeyResponse { Key = key };
    }

    public async Task<bool> AuthenticateAsync(string agentId, string secret, CancellationToken ct = default)
    {
        var agent = await agents.FindByIdAsync(agentId, ct);
        return agent is not null && AgentSecrets.Verify(secret, agent.SecretHash);
    }

    public async Task MarkSeenAsync(string agentId, CancellationToken ct = default)
    {
        var agent = await agents.FindByIdAsync(agentId, ct);
        if (agent is null)
        {
            return;
        }

        agent.LastSeenAt = clock.GetUtcNow();
        await agents.UpdateAsync(agent, ct);
    }

    private AgentResponse Map(Agent a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Platform = a.Platform,
        Status = registry.IsOnline(a.Id) ? AgentStatus.Online : AgentStatus.Offline,
        Metrics = registry.MetricsFor(a.Id),
        EnrolledAt = a.EnrolledAt,
        LastSeenAt = a.LastSeenAt,
    };
}
