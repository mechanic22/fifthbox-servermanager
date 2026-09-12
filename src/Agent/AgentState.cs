using System.Text.Json;
using FifthBox.ServerManager.Shared.Agents;

namespace FifthBox.ServerManager.Agent;

/// One workload the agent had running when it last wrote its state file.
public sealed class TrackedWorkload
{
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }

    /// The process's own start time — checked on adoption, because the OS reuses PIDs.
    public DateTimeOffset StartedAtUtc { get; set; }

    /// The spec, minus its environment — env values are secrets and this file is plaintext on disk.
    /// Enough survives to keep supervising (restart policy, stop grace, stop command) until the Host
    /// hands back the authoritative spec on the next reconcile.
    public AgentWorkloadSpec? Spec { get; set; }

    /// Hash of the *full* spec as it was when the process started, including the env this file drops.
    /// Adoption compares this, so a stripped spec still recognises its own process.
    public string? ConfigHash { get; set; }
}

/// The agent's process table, persisted so a restarted agent can re-attach to workloads that are still
/// running instead of orphaning them and starting duplicates.
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
            // Losing the table costs adoption on the next start, not correctness now.
        }
    }
}
