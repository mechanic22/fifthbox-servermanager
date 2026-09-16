using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

public static class DeployPermission
{
    /// operate can deploy only when already running exactly what's saved
    /// hasRevision matters, HasPendingChanges is false for never-deployed so the first draft could ship
    public static bool Allowed(AccessLevel level, bool hasRevision, bool hasPendingChanges)
        => level >= AccessLevel.Configure
        || (level >= AccessLevel.Operate && hasRevision && !hasPendingChanges);
}
