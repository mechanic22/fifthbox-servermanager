namespace FifthBox.ServerManager.Shared.Nodes;

/// Operator-set schedulability. Active takes work; Pause/Drain don't.
public enum NodeAvailability
{
    Unknown,
    Active,
    Pause,
    Drain,
}
