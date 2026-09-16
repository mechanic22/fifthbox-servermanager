namespace FifthBox.ServerManager.Shared.Access;

/// ordered so checks are a comparison, admins skip this and never hold a grant
/// values are spaced so a new level can slot in between
public enum AccessLevel
{
    None = 0,

    /// details, status, logs, history, routes
    View = 10,

    /// restart, stop, scale, and deploy only when nothing's unpublished
    Operate = 20,

    /// edit config, revert, move, deploy whenever
    Configure = 30,
}
