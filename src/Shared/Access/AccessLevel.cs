namespace FifthBox.ServerManager.Shared.Access;

/// What a user may do with a workload. Ordered — each level includes the ones below it, so
/// enforcement is a comparison. Admins bypass this entirely and never hold a grant.
/// Values are spaced so a level can be slotted in between two existing ones later.
public enum AccessLevel
{
    None = 0,

    /// See it: details, status, logs, deployment history, its routes.
    View = 10,

    /// Day-to-day running: restart, stop, scale. Deploy only when there's nothing unpublished.
    Operate = 20,

    /// Edit the saved config, revert to an old revision, move it, deploy whenever.
    Configure = 30,
}
