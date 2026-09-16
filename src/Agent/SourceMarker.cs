using System.Text.Json;

namespace FifthBox.ServerManager.Agent;

/// only written on success, a half-done acquire leaves no marker
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
            // only costs the version display
        }
    }
}
