namespace FifthBox.ServerManager.Shared.Workloads;

/// Where a workload's files come from, as sent to the agent that fetches them. The Steam password is
/// plaintext here and nowhere else — it is decrypted just before the spec goes out.
public record WorkloadSourceSpec
{
    public SourceKind Kind { get; init; }

    /// Archive location, for Zip.
    public string? Url { get; init; }

    public int? SteamAppId { get; init; }

    /// Steam branch, when it isn't the default public one.
    public string? SteamBranch { get; init; }

    /// Blank means an anonymous login, which is what most dedicated servers use.
    public string? SteamUsername { get; init; }
    public string? SteamPassword { get; init; }
}

/// What a client sends when configuring a source. A blank SteamPassword leaves the stored one alone, so
/// editing anything else never has to round-trip the secret.
public record WorkloadSourceRequest
{
    public SourceKind Kind { get; init; }
    public string? Url { get; init; }
    public int? SteamAppId { get; init; }
    public string? SteamBranch { get; init; }
    public string? SteamUsername { get; init; }
    public string? SteamPassword { get; init; }
}

/// What a client is told about a source. No password, ever — only whether one is set.
public record WorkloadSourceResponse
{
    public SourceKind Kind { get; init; }
    public string? Url { get; init; }
    public int? SteamAppId { get; init; }
    public string? SteamBranch { get; init; }
    public string? SteamUsername { get; init; }
    public bool HasSteamPassword { get; init; }
}
