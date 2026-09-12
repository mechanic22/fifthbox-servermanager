namespace FifthBox.ServerManager.Shared.Workloads;

/// Where a native workload's files come from. A source is an acquire step that runs before the run step,
/// on its own command — never as part of a deploy, because acquiring can take a very long time.
public enum SourceKind
{
    /// Someone installed it on the machine already; the agent only runs it.
    None,

    /// Downloaded and extracted from an archive URL.
    Zip,

    /// Installed and updated by SteamCMD from a Steam app id.
    SteamCmd,
}
