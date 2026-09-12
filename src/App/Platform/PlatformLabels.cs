namespace FifthBox.ServerManager.App.Platform;

/// The swarm label ServerManager stamps on its own infrastructure services (nginx today; letsencrypt
/// later) so the System view can list and manage them separately from user workloads.
public static class PlatformLabels
{
    public const string RoleKey = "fbsm.role";
    public const string PlatformRole = "platform";

    public static readonly IReadOnlyDictionary<string, string> Marker =
        new Dictionary<string, string> { [RoleKey] = PlatformRole };
}
