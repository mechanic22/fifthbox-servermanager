using System.Text.Json;

namespace FifthBox.ServerManager.Agent;

/// The agent's persisted identity after enrollment. The secret is a bearer credential — written to a
/// file with owner-only permissions on Unix.
public sealed class AgentCredentials
{
    public string AgentId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    public static AgentCredentials? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var credentials = JsonSerializer.Deserialize<AgentCredentials>(File.ReadAllText(path));
            return string.IsNullOrEmpty(credentials?.AgentId) ? null : credentials;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this));

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception)
            {
                // Best effort — permissions hardening isn't fatal.
            }
        }
    }
}
