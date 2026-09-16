namespace FifthBox.ServerManager.Shared.Workloads;

/// only place the steam password is plaintext, decrypted just before it goes out
public record WorkloadSourceSpec
{
    public SourceKind Kind { get; init; }

    /// zip only
    public string? Url { get; init; }

    public int? SteamAppId { get; init; }

    public string? SteamBranch { get; init; }

    /// blank = anonymous login, what most dedicated servers use
    public string? SteamUsername { get; init; }
    public string? SteamPassword { get; init; }
}

/// blank SteamPassword keeps the stored one
public record WorkloadSourceRequest
{
    public SourceKind Kind { get; init; }
    public string? Url { get; init; }
    public int? SteamAppId { get; init; }
    public string? SteamBranch { get; init; }
    public string? SteamUsername { get; init; }
    public string? SteamPassword { get; init; }
}

public record WorkloadSourceResponse
{
    public SourceKind Kind { get; init; }
    public string? Url { get; init; }
    public int? SteamAppId { get; init; }
    public string? SteamBranch { get; init; }
    public string? SteamUsername { get; init; }
    public bool HasSteamPassword { get; init; }
}
