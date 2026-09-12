using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

public static class DeployPermission
{
    /// Deploy publishes the saved config, so Operate gets it only for a workload that is already
    /// running exactly what's saved — bouncing the running config, never shipping someone else's
    /// unfinished edits. The hasRevision check carries its weight: HasPendingChanges is false for a
    /// workload that was never deployed, so without it an operator could ship the first draft.
    public static bool Allowed(AccessLevel level, bool hasRevision, bool hasPendingChanges)
        => level >= AccessLevel.Configure
        || (level >= AccessLevel.Operate && hasRevision && !hasPendingChanges);
}
