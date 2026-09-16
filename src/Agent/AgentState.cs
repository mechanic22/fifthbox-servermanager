using System.Text.Json;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

public sealed class TrackedWorkload
{
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }

    /// checked on adoption because the os reuses pids
    public DateTimeOffset StartedAtUtc { get; set; }

    /// no env, it's secrets and this file is plaintext. enough to supervise until the next reconcile
    public AgentWorkloadSpec? Spec { get; set; }

    /// hash of the full spec incl env, so a stripped spec still matches its process
    public string? ConfigHash { get; set; }
}

public sealed class AgentState
{
    public List<TrackedWorkload> Workloads { get; set; } = [];

    public static AgentState Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AgentState();
        }

        try
        {
            return JsonSerializer.Deserialize<AgentState>(File.ReadAllText(path)) ?? new AgentState();
        }
        catch (Exception)
        {
            return new AgentState();
        }
    }

    public void Save(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this));

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception)
        {
            // losing it just costs adoption on next start
        }
    }
}
