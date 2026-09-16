using System.Text.Json;

namespace FifthBox.ServerManager.Agent;

/// the secret is a bearer credential, owner-only file on unix
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
                // best effort
            }
        }
    }
}
