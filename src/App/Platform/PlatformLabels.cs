namespace FifthBox.ServerManager.App.Platform;

public static class PlatformLabels
{
    public const string RoleKey = "fbsm.role";
    public const string PlatformRole = "platform";

    public static readonly IReadOnlyDictionary<string, string> Marker =
        new Dictionary<string, string> { [RoleKey] = PlatformRole };
}
