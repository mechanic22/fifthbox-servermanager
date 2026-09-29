using FifthBox.ServerManager.Shared.Access;

namespace FifthBox.ServerManager.App.Access;

public static class DeployPermission
{
    /// deploying publishes config, so it's Configure. operate brings a stopped workload back with Start,
    /// which redeploys the running revision rather than whatever is saved
    public static bool Allowed(AccessLevel level) => level >= AccessLevel.Configure;
}
