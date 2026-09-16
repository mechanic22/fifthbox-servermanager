namespace FifthBox.ServerManager.App.Certificates;

public sealed class AcmeOptions
{
    /// staging by default on purpose, prod allows 5 dupes a week and a wiring bug can lock us out for days
    public string Directory { get; set; } = "staging";

    public int ValidationTimeoutSeconds { get; set; } = 60;

    /// ~30 daily attempts before it matters, so a missed tick barely counts
    public int RenewBeforeDays { get; set; } = 30;

    /// added to the built-in reserved set, not a replacement
    public List<string> ReservedSuffixes { get; set; } = [.. IssuableHostname.ReservedSuffixes];
}
