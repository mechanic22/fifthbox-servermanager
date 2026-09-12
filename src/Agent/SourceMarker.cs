using System.Text.Json;

namespace FifthBox.ServerManager.Agent;

/// What the last successful acquire left in a workload's directory. Written only on success, so a
/// half-finished acquire leaves no marker and the workload reports no version until it is run again.
public sealed class SourceMarker
{
    public const string FileName = ".fbsm-source.json";

    public string Version { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; set; }

    public static string? Read(string installRoot)
    {
        try
        {
            var path = Path.Combine(installRoot, FileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SourceMarker>(File.ReadAllText(path))?.Version
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Write(string installRoot, string version)
    {
        try
        {
            var marker = new SourceMarker { Version = version, AcquiredAt = DateTimeOffset.UtcNow };
            File.WriteAllText(Path.Combine(installRoot, FileName), JsonSerializer.Serialize(marker));
        }
        catch (Exception)
        {
            // The files are there either way; losing the marker costs the version display, not the install.
        }
    }
}
