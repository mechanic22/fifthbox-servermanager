namespace FifthBox.ServerManager.App.Workloads;

/// deliberately not the whole spec, the backend normalises it and a full compare drifts constantly
public record DeployedSpec
{
    public required string Image { get; init; }
    public int Replicas { get; init; }

    public IReadOnlyList<string> Env { get; init; } = [];

    /// "published:target/protocol", however the backend spells it
    public IReadOnlyList<string> Ports { get; init; } = [];

    /// what the containers are really bound to, host-mode ports can lag the spec
    /// empty when the backend can't say
    public IReadOnlyList<string> RunningPorts { get; init; } = [];
}

public interface IDeployedSpecSource
{
    /// null means not deployed, not drift
    Task<DeployedSpec?> GetAsync(string serviceName, CancellationToken ct = default);
}
