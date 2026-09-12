using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

/// A workload's source configuration, persisted as one JSON column. Grouped rather than spread across
/// columns so a new provider adds fields here instead of another row of carry-through everywhere a
/// workload is copied.
public class WorkloadSource
{
    public SourceKind Kind { get; set; }
    public string? Url { get; set; }

    public int? SteamAppId { get; set; }
    public string? SteamBranch { get; set; }
    public string? SteamUsername { get; set; }

    /// Encrypted at rest, never returned to a client, decrypted only when the spec goes to an agent.
    public string? SteamPasswordEnc { get; set; }

    public WorkloadSource Copy() => new()
    {
        Kind = Kind,
        Url = Url,
        SteamAppId = SteamAppId,
        SteamBranch = SteamBranch,
        SteamUsername = SteamUsername,
        SteamPasswordEnc = SteamPasswordEnc,
    };
}
