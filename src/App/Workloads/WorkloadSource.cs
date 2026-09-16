using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.App.Workloads;

public class WorkloadSource
{
    public SourceKind Kind { get; set; }
    public string? Url { get; set; }

    public int? SteamAppId { get; set; }
    public string? SteamBranch { get; set; }
    public string? SteamUsername { get; set; }

    /// encrypted, never returned, decrypted only when the spec goes to an agent
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
