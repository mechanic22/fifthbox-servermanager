namespace FifthBox.ServerManager.Shared.Nodes;

public enum NodePlatform
{
    Unknown,
    Linux,
    Windows,

    /// Agent nodes only — a swarm node is never macOS.
    MacOS,
}
