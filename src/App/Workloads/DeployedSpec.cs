namespace FifthBox.ServerManager.App.Workloads;

/// The parts of a running service worth comparing against what we think we deployed. Deliberately not
/// the whole spec: the backend fills in defaults, resolves tags to digests and normalises half of it, so
/// a full comparison would report drift constantly. These four are what a hand-run update changes.
public record DeployedSpec
{
    public required string Image { get; init; }
    public int Replicas { get; init; }

    /// KEY=VALUE as the backend holds them.
    public IReadOnlyList<string> Env { get; init; } = [];

    /// "published:target/protocol", however the backend spells it.
    public IReadOnlyList<string> Ports { get; init; } = [];

    /// The ports the running instances are really bound to, same spelling. Swarm publishes a host-mode
    /// port from the container, so a service can hold the spec we asked for while the container that has
    /// been up for a day answers on the port it was created with. Empty when the backend can't say.
    public IReadOnlyList<string> RunningPorts { get; init; } = [];
}

/// Port: read back what a backend is actually running, as opposed to what we recorded deploying.
public interface IDeployedSpecSource
{
    /// Null when there's no such service — that's "not deployed", not drift.
    Task<DeployedSpec?> GetAsync(string serviceName, CancellationToken ct = default);
}
