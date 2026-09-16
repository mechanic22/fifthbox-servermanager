namespace FifthBox.ServerManager.Shared.Workloads;

/// acquire runs on its own command, never in a deploy, it can take ages
public enum SourceKind
{
    /// already installed, the agent just runs it
    None,

    Zip,

    SteamCmd,
}
